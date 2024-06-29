using System.Runtime.InteropServices;
using HarmonyDB.Index.Analysis.Models.CompactV1;

namespace HarmonyDB.Index.Analysis.Models;

public class Loop
{
    public required int SequenceIndex { get; init; }
    public required int Start { get; init; }
    public required int EndMovement { get; init; }
    public required int Occurrences { get; init; }
    public required int Successions { get; init; }
    public required HashSet<int> Coverage { get; init; }
    public required HashSet<int> FoundFirsts { get; init; }
    public required ReadOnlyMemory<CompactHarmonyMovement> Progression { get; init; }
    public required bool IsCompound { get; init; }

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

    public ReadOnlyMemory<CompactHarmonyMovement> GetNormalizedProgression()
    {
        var buffer = new byte[4];
        var idSequences = MemoryMarshal.ToEnumerable(Progression)
            .Select(p =>
            {
                buffer[0] = p.RootDelta;
                buffer[1] = (byte)p.FromType;
                buffer[2] = (byte)p.ToType;
                return BitConverter.ToInt32(buffer);
            })
            .ToArray();

        var shifts = Enumerable.Range(0, Length).ToList();
        var iteration = 0;
        while (shifts.Count > 1)
        {
            if (iteration == Length) // multiple shifts possible
            {
                shifts = [shifts.First()];
                break;
            }

            shifts = shifts
                .GroupBy(s => idSequences[(s + iteration) % Length])
                .MinBy(g => g.Key)!
                .ToList();

            iteration++;
        }

        return Enumerable.Range(shifts.Single(), Length)
            .Select(s => Progression.Span[s % Length])
            .ToArray();
    }
}