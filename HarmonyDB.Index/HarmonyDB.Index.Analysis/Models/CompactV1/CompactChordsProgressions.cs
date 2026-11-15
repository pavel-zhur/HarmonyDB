using System.Buffers.Binary;
using HarmonyDB.Index.Analysis.Models.Ids;

namespace HarmonyDB.Index.Analysis.Models.CompactV1;

/// <summary>
/// The progressions of many songs, each as a <see cref="CompactChordsProgression"/> holds it, in a few large arrays:
/// the songs' sequences in one array, and the movements of all the sequences in large blocks, each sequence a slice of one.
/// </summary>
public class CompactChordsProgressions
{
    private readonly IdTable _songIds;
    private readonly int[] _harmonySequenceLengths;
    private readonly int[] _sequencesStarts;
    private readonly CompactHarmonyMovementsSlice[] _sequences;

    private CompactChordsProgressions(IdTable songIds, IdPositions songs, int[] harmonySequenceLengths, int[] sequencesStarts, CompactHarmonyMovementsSlice[] sequences)
    {
        _songIds = songIds;
        Songs = songs;
        _harmonySequenceLengths = harmonySequenceLengths;
        _sequencesStarts = sequencesStarts;
        _sequences = sequences;
    }

    /// <summary>
    /// The songs (numbers of the songs' ids) in the order they were added.
    /// </summary>
    public IdPositions Songs { get; }

    public int Count => Songs.Count;

    public string GetExternalId(int position) => _songIds[Songs.GetNumber(position)];

    public int GetHarmonySequenceLength(int position) => _harmonySequenceLengths[position];

    public ArraySegment<CompactHarmonyMovementsSlice> GetSequences(int position)
        => new(_sequences, _sequencesStarts[position], _sequencesStarts[position + 1] - _sequencesStarts[position]);

    /// <summary>
    /// The progression as an object, its movements not copied.
    /// </summary>
    public CompactChordsProgression Get(int position) => new()
    {
        HarmonySequenceLength = GetHarmonySequenceLength(position),
        ExtendedHarmonyMovementsSequences = GetSequences(position).Select(x => x.ToSequence()).ToList(),
    };

    public class Builder(IdTable songIds)
    {
        private const int BlockLength = 1 << 20;

        private readonly List<int> _numbers = new();
        private readonly List<int> _harmonySequenceLengths = new();
        private readonly List<int> _sequencesStarts = new();
        private readonly List<CompactHarmonyMovementsSlice> _sequences = new();
        private CompactHarmonyMovement[] _block = [];
        private int _blockUsed;

        /// <param name="externalId">The song's external id.</param>
        /// <param name="serialized">The song's progression, as <see cref="CompactChordsProgression.Serialize"/> writes it.</param>
        public void Add(string externalId, ReadOnlySpan<byte> serialized)
        {
            _numbers.Add(songIds.GetOrAdd(externalId));
            _sequencesStarts.Add(_sequences.Count);

            _harmonySequenceLengths.Add(ReadInt32(ref serialized));
            var sequencesCount = ReadInt32(ref serialized);
            for (var i = 0; i < sequencesCount; i++)
            {
                var firstMovementFromIndex = ReadInt32(ref serialized);
                var firstRoot = ReadByte(ref serialized);
                var movements = Allocate(ReadInt32(ref serialized));
                var span = movements.Span;
                for (var j = 0; j < span.Length; j++)
                {
                    span[j] = new()
                    {
                        RootDelta = ReadByte(ref serialized),
                        FromType = (ChordType)ReadByte(ref serialized),
                        ToType = (ChordType)ReadByte(ref serialized),
                    };
                }

                _sequences.Add(new(movements, firstMovementFromIndex, firstRoot));
            }
        }

        public CompactChordsProgressions Build() => new(
            songIds,
            new(_numbers.ToArray()),
            _harmonySequenceLengths.ToArray(),
            _sequencesStarts.Append(_sequences.Count).ToArray(),
            _sequences.ToArray());

        // A place for the movements of one sequence: in the current block, or in a new one when it does not fit.
        private Memory<CompactHarmonyMovement> Allocate(int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(length);

            if (length > BlockLength) return new CompactHarmonyMovement[length];

            if (_blockUsed + length > _block.Length)
            {
                _block = new CompactHarmonyMovement[BlockLength];
                _blockUsed = 0;
            }

            var result = _block.AsMemory(_blockUsed, length);
            _blockUsed += length;
            return result;
        }

        private static int ReadInt32(ref ReadOnlySpan<byte> data)
        {
            var result = BinaryPrimitives.ReadInt32LittleEndian(data);
            data = data[sizeof(int)..];
            return result;
        }

        private static byte ReadByte(ref ReadOnlySpan<byte> data)
        {
            var result = data[0];
            data = data[1..];
            return result;
        }
    }
}
