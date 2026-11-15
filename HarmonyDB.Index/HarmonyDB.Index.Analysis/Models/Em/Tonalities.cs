using System.Buffers.Binary;
using System.Text;
using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Models.Ids;

namespace HarmonyDB.Index.Analysis.Models.Em;

/// <summary>
/// The tonalities of the loops and of the songs, as <see cref="EmModel.Serialize"/> writes them, in arrays.
/// </summary>
public class Tonalities
{
    private const int ProbabilitiesLength = Constants.TonicCount * Constants.ScaleCount;
    private const byte UnknownTonality = 255;

    private Tonalities(TonalitySources loops, TonalitySongs songs)
    {
        Loops = loops;
        Songs = songs;
    }

    public TonalitySources Loops { get; }

    public TonalitySongs Songs { get; }

    public static Tonalities Read(Stream stream, IndexIds ids)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        var loops = ReadSources(reader, ids.Loops, false);
        var songs = ReadSources(reader, ids.Songs, true);

        return new(
            new(ids.Loops, new(loops.numbers), loops.scores, loops.probabilities),
            new(ids.Songs, new(songs.numbers), songs.scores, songs.probabilities, songs.knownTonalities!));
    }

    private static (int[] numbers, float[] scores, float[] probabilities, byte[]? knownTonalities) ReadSources(BinaryReader reader, IdTable ids, bool areSongs)
    {
        var count = reader.ReadInt32();
        var numbers = new int[count];
        var scores = new float[count * 2];
        var probabilities = new float[count * ProbabilitiesLength];
        var knownTonalities = areSongs ? new byte[count] : null;
        Span<byte> data = stackalloc byte[(2 + ProbabilitiesLength) * sizeof(float)];
        for (var position = 0; position < count; position++)
        {
            numbers[position] = ids.GetOrAdd(reader.ReadString());

            if (knownTonalities != null)
            {
                knownTonalities[position] = reader.ReadByte();
            }

            // the scores, then the probabilities [tonic, scale] tonic after tonic: the order of ToLinear
            reader.BaseStream.ReadExactly(data);
            scores[position * 2] = BinaryPrimitives.ReadSingleLittleEndian(data);
            scores[position * 2 + 1] = BinaryPrimitives.ReadSingleLittleEndian(data[sizeof(float)..]);
            for (var i = 0; i < ProbabilitiesLength; i++)
            {
                probabilities[position * ProbabilitiesLength + i] = BinaryPrimitives.ReadSingleLittleEndian(data[((2 + i) * sizeof(float))..]);
            }
        }

        return (numbers, scores, probabilities, knownTonalities);
    }

    /// <summary>
    /// The loops or the songs with their tonalities, in their order.
    /// </summary>
    public class TonalitySources
    {
        private readonly IdTable _ids;
        private readonly float[] _scores;
        private readonly float[] _probabilities;

        internal TonalitySources(IdTable ids, IdPositions positions, float[] scores, float[] probabilities)
        {
            _ids = ids;
            Positions = positions;
            _scores = scores;
            _probabilities = probabilities;
        }

        public IdPositions Positions { get; }

        public int Count => Positions.Count;

        public string GetId(int position) => _ids[Positions.GetNumber(position)];

        public (float TonicScore, float ScaleScore) GetScore(int position) => (_scores[position * 2], _scores[position * 2 + 1]);

        /// <summary>
        /// The probabilities of the tonalities, as <see cref="Tools.TonalitiesExtensions.ToLinear"/> gives them.
        /// </summary>
        public float[] GetProbabilities(int position) => _probabilities.AsSpan(position * ProbabilitiesLength, ProbabilitiesLength).ToArray();
    }

    /// <summary>
    /// The songs with their tonalities, and the tonalities known for them.
    /// </summary>
    public class TonalitySongs : TonalitySources
    {
        private readonly byte[] _knownTonalities;

        internal TonalitySongs(IdTable ids, IdPositions positions, float[] scores, float[] probabilities, byte[] knownTonalities)
            : base(ids, positions, scores, probabilities)
        {
            _knownTonalities = knownTonalities;
        }

        public (byte Tonic, Scale Scale)? GetKnownTonality(int position)
            => _knownTonalities[position] == UnknownTonality
                ? null
                : ((byte)(_knownTonalities[position] / 2), (Scale)(_knownTonalities[position] % 2));
    }
}
