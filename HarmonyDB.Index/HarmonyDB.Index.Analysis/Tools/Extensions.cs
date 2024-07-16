using HarmonyDB.Index.Analysis.Models;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Tools;

public static class Extensions
{
    public static string ChordTypeToString(this ChordType chordType, ChordTypePresentation presentation = ChordTypePresentation.Default) =>
        chordType switch
        {
            ChordType.Major => presentation == ChordTypePresentation.MajorAsM ? "M" : string.Empty,
            ChordType.Minor => presentation != ChordTypePresentation.Degrees ? "m" : string.Empty,
            ChordType.Power => "5",
            ChordType.Sus2 => "sus2",
            ChordType.Sus4 => "sus4",
            ChordType.Diminished => "dim",
            ChordType.Augmented => "aug",
            ChordType.Unknown => "?",
            _ => throw new ArgumentOutOfRangeException(nameof(chordType), chordType, "Unexpected chord type."),
        };

    public static IEnumerable<T> ShiftLoop<T>(this IEnumerable<T> source, bool shiftLoop, Func<T, int> wantsToGoFirst, out int? shift)
    {
        shift = null;
        if (!shiftLoop)
            return source;

        var list = source.ToList();
        shift = list.WithIndices().OrderByDescending(x => wantsToGoFirst(x.x)).ThenBy(x => x.i).First().i;
        return list.Concat(list).Skip(shift.Value).Take(list.Count);
    }

    public static IEnumerable<T> Loopify<T>(this IEnumerable<T> source, bool loopify)
    {
        T? first = default;
        foreach (var (item, isFirst) in source.WithIsFirst())
        {
            if (isFirst)
            {
                first = item;
            }

            yield return item;
        }

        if (loopify)
        {
            yield return first!;
        }
    }
}