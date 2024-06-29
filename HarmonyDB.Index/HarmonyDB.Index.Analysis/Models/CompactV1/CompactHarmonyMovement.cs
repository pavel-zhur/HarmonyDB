using HarmonyDB.Index.Analysis.Models.Interfaces;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Models.CompactV1;

public record struct CompactHarmonyMovement : ISearchableHarmonyMovement
{
    public required byte RootDelta { get; init; }

    public required ChordType FromType { get; init; }

    public required ChordType ToType { get; init; }

    public static bool Equals(ref readonly CompactHarmonyMovement first, ref readonly CompactHarmonyMovement second) =>
        second.RootDelta == first.RootDelta && second.FromType == first.FromType && second.ToType == first.ToType;

    public static bool AreNormalizedEqual(ReadOnlyMemory<CompactHarmonyMovement> progression1,
        ReadOnlyMemory<CompactHarmonyMovement> progression2)
        => AreEqual(progression1, progression2, fixedShift: 0) switch
        {
            0 => true,
            null => false,
            _ => throw new("Could not have happened."),
        };

    public static int? AreEqual(ReadOnlyMemory<CompactHarmonyMovement> progression1,
        ReadOnlyMemory<CompactHarmonyMovement> progression2, int? shifts = null, int? fixedShift = null)
    {
        var length = progression1.Length;
        if (length != progression2.Length) return null;

        shifts ??= length;

        foreach (var shift in fixedShift.HasValue ? fixedShift.Value.Once() : Enumerable.Range(0, shifts.Value))
        {
            var failed = false;
            for (var i = 0; i < length; i++)
            {
                var j = (i + shift) % length;
                if (!Equals(in progression2.Span[j], in progression1.Span[i]))
                {
                    failed = true;
                    break;
                }
            }

            if (!failed)
            {
                return shift;
            }
        }

        return null;
    }
}