using System.Runtime.InteropServices;
using HarmonyDB.Index.Analysis.Models.CompactV1;

namespace HarmonyDB.Index.Analysis.Models;

public record Loop
{
    public required int SequenceIndex { get; init; }
    public required int Start { get; init; }
    public required int Occurrences { get; init; }
    public required int Successions { get; init; }
    public required HashSet<int> Coverage { get; init; }
    public required HashSet<int> FoundFirsts { get; init; }
    public required ReadOnlyMemory<CompactHarmonyMovement> Progression { get; init; }

    [Obsolete]
    public required bool IsCompound { get; init; }

    public int EndMovement => Start + Length - 1;
    public int Length => Progression.Length;

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
            bytes[index * 3] = progression.Span[index].RootDelta;
            bytes[index * 3 + 1] = (byte)progression.Span[index].FromType;
            bytes[index * 3 + 2] = (byte)progression.Span[index].ToType;
        }

        return Convert.ToBase64String(bytes);
    }

    public ReadOnlyMemory<CompactHarmonyMovement> GetNormalizedProgression() => GetNormalizedProgression(Progression, out _, out _);

    /// <param name="normalizedShift">
    /// Between 0 (inclusive) and progression length (exclusive).
    /// Returns the number of steps the normalized progression start is behind the original progression start.
    /// In other words, returns the index of the start of the original progression in the normalized progression.
    /// For invariants > 0, returns the minimal possible shift.
    /// </param>
    public ReadOnlyMemory<CompactHarmonyMovement> GetNormalizedProgression(out int normalizationShift) => GetNormalizedProgression(Progression, out normalizationShift, out _);

    public static int InvertNormalizationShift(int normalizationShift, int progressionLength) =>
        (progressionLength - normalizationShift) % progressionLength;

    /// <param name="normalizedShift">
    /// Between 0 (inclusive) and progression length (exclusive).
    /// Returns the number of steps the normalized progression start is behind the original progression start.
    /// In other words, returns the index of the start of the original progression in the normalized progression.
    /// For invariants > 0, returns the minimal possible shift.
    /// </param>
    public static ReadOnlyMemory<CompactHarmonyMovement> GetNormalizedProgression(ReadOnlyMemory<CompactHarmonyMovement> progression, out int normalizationShift, out int invariants)
    {
        var buffer = new byte[4];
        var idSequences = MemoryMarshal.ToEnumerable(progression)
            .Select(p =>
            {
                buffer[0] = p.RootDelta;
                buffer[1] = (byte)p.FromType;
                buffer[2] = (byte)p.ToType;
                return BitConverter.ToInt32(buffer);
            })
            .ToArray();

        var length = progression.Length;
        var shifts = Enumerable.Range(0, length).ToList();
        var iteration = 0;
        invariants = 1;
        while (shifts.Count > 1)
        {
            if (iteration == length) // multiple shifts possible
            {
                invariants = shifts.Count;
                shifts = [shifts.Last()];
                break;
            }

            shifts = shifts
                .GroupBy(s => idSequences[(s + iteration) % length])
                .MinBy(g => g.Key)!
                .ToList();

            iteration++;
        }

        normalizationShift = InvertNormalizationShift(shifts.Single(), length);
        return Enumerable.Range(shifts.Single(), length)
            .Select(s => progression.Span[s % length])
            .ToArray();
    }
}