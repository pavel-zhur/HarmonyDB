using HarmonyDB.Index.Analysis.Models.CompactV1;
using Convert = System.Convert;

namespace HarmonyDB.Index.Analysis.Models;

public class Loop
{
    private ReadOnlyMemory<CompactHarmonyMovement>? _normalizedProgression;

    public required int SequenceIndex { get; init; }
    public required int Start { get; init; }
    public required int EndMovement { get; init; }
    public required int Occurrences { get; init; }
    public required int Successions { get; init; }
    public required HashSet<int> Coverage { get; init; }
    public required HashSet<int> FoundFirsts { get; init; }
    public required ReadOnlyMemory<CompactHarmonyMovement> Progression { get; init; }
    public required bool IsCompound { get; init; }

    public static ReadOnlyMemory<CompactHarmonyMovement> Deserialize(string progression)
    {
        var bytes = Convert.FromBase64String(progression);
        var result = new CompactHarmonyMovement[bytes.Length / 3];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new()
            {
                RootDelta = bytes[i * 3],
                FromType = (ChordType)bytes[i * 3 + 1],
                ToType = (ChordType)bytes[i * 3 + 2],
            };
        }

        return result;
    }

    public static string Serialize(ReadOnlyMemory<CompactHarmonyMovement> progression)
    {
        var bytes = new byte[progression.Length * 3];
        for (var index = 0; index < progression.Span.Length; index++)
        {
            bytes[index * 3] = (byte)progression.Span[index].RootDelta;
            bytes[index * 3 + 1] = (byte)progression.Span[index].FromType;
            bytes[index * 3 + 2] = (byte)progression.Span[index].ToType;
        }

        return Convert.ToBase64String(bytes);
    }

    public ReadOnlyMemory<CompactHarmonyMovement> GetNormalizedProgression()
    {
        if (_normalizedProgression == null)
        {
            var array = Progression.ToArray();
            var length = array.Length;

            IEnumerable<CompactHarmonyMovement> Shift(int i) => array[i..].Concat(array[..i]);

            int[] CreateIdSequence(int shift) => Shift(shift).Select(x => BitConverter.ToInt32(new byte[]
            {
                (byte)x.RootDelta,
                (byte)x.FromType,
                (byte)x.ToType,
                0,
            })).ToArray();

            var options = Enumerable.Range(0, length - 1)
                .Select(shift => (idSequence: CreateIdSequence(shift), shift))
                .ToList();

            var bestShift = Enumerable.Range(0, length - 1).Aggregate(options.OrderBy(o => o.idSequence[0]),
                    (s, a) => s.ThenByDescending(x => x.idSequence[a]))
                .First().shift;

            _normalizedProgression = Shift(bestShift).ToArray();
        }

        return _normalizedProgression.Value;
    }
}