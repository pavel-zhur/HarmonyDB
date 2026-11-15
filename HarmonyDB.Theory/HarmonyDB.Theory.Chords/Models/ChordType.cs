using HarmonyDB.Theory.Chords.Models.Enums;

namespace HarmonyDB.Theory.Chords.Models;

/// <summary>
/// A chord type. Parsed chord types are normalized (see <see cref="Normalize"/>): two notations of the same chord give equal chord types.
/// The half-diminished chord is <see cref="ChordMainType.Dim"/> with <see cref="ChordTypeExtension.X7"/>.
/// The augmented seventh chord is <see cref="ChordMainType.Aug"/> with <see cref="ChordTypeExtension.X7"/>.
/// The eleventh chords include the ninth; the thirteenth chords include the ninth but not the eleventh.
/// </summary>
public record ChordType(ChordMainType Type, ChordTypeExtension? Extension, ChordTypeAdditions Additions)
{
    private static readonly IReadOnlyList<(ChordTypeAdditions addition, byte interval)> AdditionIntervals =
    [
        (ChordTypeAdditions.Add2, 2),
        (ChordTypeAdditions.Flat2, 1),
        (ChordTypeAdditions.Add4, 5),
        (ChordTypeAdditions.Sharp4, 6),
        (ChordTypeAdditions.Add6, 9),
        (ChordTypeAdditions.Flat6, 8),
        (ChordTypeAdditions.Add9, 2),
        (ChordTypeAdditions.Flat9, 1),
        (ChordTypeAdditions.Sharp9, 3),
        (ChordTypeAdditions.Add11, 5),
        (ChordTypeAdditions.Flat11, 4),
        (ChordTypeAdditions.Sharp11, 6),
        (ChordTypeAdditions.Add13, 9),
        (ChordTypeAdditions.Flat13, 8),
        (ChordTypeAdditions.Sharp13, 10),
    ];

    /// <summary>
    /// The pitch classes of the chord, as semitones above the root, ascending, the root (0) first.
    /// </summary>
    public IReadOnlyList<byte> GetIntervals()
    {
        var result = new SortedSet<byte> { 0 };

        // The third.
        switch (Type)
        {
            case ChordMainType.Major or ChordMainType.Aug:
                result.Add(4);
                break;
            case ChordMainType.Minor or ChordMainType.Dim or ChordMainType.Dim7:
                result.Add(3);
                break;
            case ChordMainType.Sus2:
                result.Add(2);
                break;
            case ChordMainType.Sus4:
                result.Add(5);
                break;
        }

        // The fifth.
        if (!Additions.HasFlag(ChordTypeAdditions.No5))
        {
            var flat = Additions.HasFlag(ChordTypeAdditions.Flat5) || Type is ChordMainType.Dim or ChordMainType.Dim7;
            var sharp = Additions.HasFlag(ChordTypeAdditions.Sharp5) || Type == ChordMainType.Aug;
            if (flat) result.Add(6);
            if (sharp) result.Add(8);
            if (!flat && !sharp) result.Add(7);
        }

        // The seventh.
        if (Type == ChordMainType.Dim7)
            result.Add(9);

        if (Extension.HasValue && !Additions.HasFlag(ChordTypeAdditions.No7))
            result.Add(Extension.Value >= ChordTypeExtension.XMaj7 ? (byte)11 : (byte)10);

        // The upper degrees implied by the extension, unless altered or omitted.
        var upper = Extension switch
        {
            ChordTypeExtension.X9 or ChordTypeExtension.XMaj9 => 9,
            ChordTypeExtension.X11 or ChordTypeExtension.XMaj11 => 11,
            ChordTypeExtension.X13 or ChordTypeExtension.XMaj13 => 13,
            _ => 7,
        };

        if (upper >= 9 && !Additions.HasFlag(ChordTypeAdditions.No9) && (Additions & (ChordTypeAdditions.Flat9 | ChordTypeAdditions.Sharp9)) == 0)
            result.Add(2);

        if (upper == 11 && !Additions.HasFlag(ChordTypeAdditions.No11) && (Additions & (ChordTypeAdditions.Flat11 | ChordTypeAdditions.Sharp11)) == 0)
            result.Add(5);

        if (upper == 13 && (Additions & (ChordTypeAdditions.Flat13 | ChordTypeAdditions.Sharp13)) == 0)
            result.Add(9);

        foreach (var (addition, interval) in AdditionIntervals)
        {
            if (Additions.HasFlag(addition))
                result.Add(interval);
        }

        return result.ToList();
    }

    /// <summary>
    /// The same chord in the normal form, so that two notations of the same chord give equal chord types:
    /// the degrees that sound the same pitch class are named one way (2 and 9, 4 and 11, 6 and 13),
    /// a minor triad with a flat fifth is diminished, a major triad with a sharp fifth is augmented,
    /// an added degree implied by the extension is merged into the extension.
    /// </summary>
    public ChordType Normalize()
    {
        var type = Type;
        var extension = Extension;
        var additions = Additions;

        void Replace(ChordTypeAdditions from, ChordTypeAdditions to)
        {
            if (!additions.HasFlag(from)) return;
            additions &= ~from;
            additions |= to;
        }

        bool Take(ChordTypeAdditions addition)
        {
            if (!additions.HasFlag(addition)) return false;
            additions &= ~addition;
            return true;
        }

        Replace(ChordTypeAdditions.Add2, ChordTypeAdditions.Add9);
        Replace(ChordTypeAdditions.Flat2, ChordTypeAdditions.Flat9);
        Replace(ChordTypeAdditions.Add4, ChordTypeAdditions.Add11);
        Replace(ChordTypeAdditions.Sharp4, ChordTypeAdditions.Sharp11);

        if (type == ChordMainType.Minor && additions.HasFlag(ChordTypeAdditions.Flat5))
        {
            type = ChordMainType.Dim;
            Take(ChordTypeAdditions.Flat5);
        }

        if (type == ChordMainType.Major && additions.HasFlag(ChordTypeAdditions.Sharp5))
        {
            type = ChordMainType.Aug;
            Take(ChordTypeAdditions.Sharp5);
        }

        if (type == ChordMainType.Aug)
            Take(ChordTypeAdditions.Sharp5);

        if (type is ChordMainType.Dim or ChordMainType.Dim7)
            Take(ChordTypeAdditions.Flat5);

        if (type == ChordMainType.Sus2 && additions.HasFlag(ChordTypeAdditions.Add11))
        {
            type = ChordMainType.Sus4;
            Take(ChordTypeAdditions.Add11);
            additions |= ChordTypeAdditions.Add9;
        }

        if (type == ChordMainType.Sus2)
            Take(ChordTypeAdditions.Add9);

        if (type == ChordMainType.Sus4)
            Take(ChordTypeAdditions.Add11);

        // The upper degrees merge into the extension: 7 + 9 = 9, 9 + 11 = 11, 9 + 13 = 13.
        if (extension.HasValue)
        {
            var major = extension.Value >= ChordTypeExtension.XMaj7;
            var degree = extension.Value switch
            {
                ChordTypeExtension.X7 or ChordTypeExtension.XMaj7 => 7,
                ChordTypeExtension.X9 or ChordTypeExtension.XMaj9 => 9,
                ChordTypeExtension.X11 or ChordTypeExtension.XMaj11 => 11,
                _ => 13,
            };

            Replace(ChordTypeAdditions.Add6, ChordTypeAdditions.Add13);
            Replace(ChordTypeAdditions.Flat6, ChordTypeAdditions.Flat13);

            if (degree == 7 && Take(ChordTypeAdditions.Add9))
                degree = 9;

            if (degree == 9 && additions.HasFlag(ChordTypeAdditions.Add13))
            {
                Take(ChordTypeAdditions.Add13);
                degree = 13;
            }

            if (degree == 9 && Take(ChordTypeAdditions.Add11))
                degree = 11;

            if (degree == 11 && Take(ChordTypeAdditions.Add13))
            {
                degree = 13;
                additions |= ChordTypeAdditions.Add11;
            }

            // The top degree of the extension, when altered, is not there: 9 with b9 is 7b9, 13 with b13 is 9b13.
            if (degree == 13 && (additions & (ChordTypeAdditions.Flat13 | ChordTypeAdditions.Sharp13)) != 0)
                degree = 9;

            if (degree == 11 && (additions & (ChordTypeAdditions.Flat11 | ChordTypeAdditions.Sharp11)) != 0)
                degree = 9;

            if (degree == 9 && (additions & (ChordTypeAdditions.Flat9 | ChordTypeAdditions.Sharp9)) != 0)
                degree = 7;

            if (degree >= 9)
                Take(ChordTypeAdditions.Add9);

            if (degree == 11)
                Take(ChordTypeAdditions.Add11);

            if (degree == 13)
                Take(ChordTypeAdditions.Add13);

            extension = (degree, major) switch
            {
                (7, false) => ChordTypeExtension.X7,
                (9, false) => ChordTypeExtension.X9,
                (11, false) => ChordTypeExtension.X11,
                (13, false) => ChordTypeExtension.X13,
                (7, true) => ChordTypeExtension.XMaj7,
                (9, true) => ChordTypeExtension.XMaj9,
                (11, true) => ChordTypeExtension.XMaj11,
                _ => ChordTypeExtension.XMaj13,
            };
        }
        else
        {
            Replace(ChordTypeAdditions.Add13, ChordTypeAdditions.Add6);
            Replace(ChordTypeAdditions.Flat13, ChordTypeAdditions.Flat6);

            // The diminished triad with the sixth sounds as the diminished seventh chord.
            if (type == ChordMainType.Dim && Take(ChordTypeAdditions.Add6))
                type = ChordMainType.Dim7;

            // The sixth is the diminished seventh itself.
            if (type == ChordMainType.Dim7)
                Take(ChordTypeAdditions.Add6);
        }

        return new(type, extension, additions);
    }

    /// <summary>
    /// The canonical name of the chord type; parsing it with the default options gives the same chord type.
    /// Expects a normalized chord type.
    /// </summary>
    public string ToCanonical()
    {
        var extension = Extension.HasValue ? new ChordTypeToken(Extension.Value).ToCanonical() : string.Empty;
        var hasSeventh = Extension.HasValue || Type == ChordMainType.Dim7;

        var result = Type switch
        {
            ChordMainType.Major => extension,
            ChordMainType.Minor => "m" + extension,
            ChordMainType.Dim when Extension.HasValue => "m" + extension + "b5",
            ChordMainType.Dim => "dim",
            ChordMainType.Aug when Extension.HasValue => extension + "#5",
            ChordMainType.Aug => "aug",
            ChordMainType.Dim7 => "dim7",
            ChordMainType.Sus2 => extension + "sus2",
            ChordMainType.Sus4 => extension + "sus4",
            // "C5" names the fifth itself, so with the fifth altered the power chord is named by the missing third.
            ChordMainType.Power when Extension.HasValue || (Additions & (ChordTypeAdditions.Flat5 | ChordTypeAdditions.Sharp5)) != 0 => extension + "no3",
            ChordMainType.Power => "5",
            _ => throw new ArgumentOutOfRangeException(),
        };

        var additions = Additions;
        if (!hasSeventh && additions.HasFlag(ChordTypeAdditions.Add6))
        {
            // The sixth chords: C6, Cm6, C6sus4.
            additions &= ~ChordTypeAdditions.Add6;
            result = Type switch
            {
                ChordMainType.Major => "6",
                ChordMainType.Minor => "m6",
                ChordMainType.Sus2 => "6sus2",
                ChordMainType.Sus4 => "6sus4",
                _ => result + "add6",
            };
        }

        foreach (var (addition, name) in new[]
                 {
                     (ChordTypeAdditions.Add9, "add9"),
                     (ChordTypeAdditions.Add11, "add11"),
                     (ChordTypeAdditions.Add13, "add13"),
                 })
        {
            if (additions.HasFlag(addition))
            {
                result += name;
                additions &= ~addition;
            }
        }

        var alterations = new[]
            {
                (ChordTypeAdditions.Flat5, "b5", false),
                (ChordTypeAdditions.Sharp5, "#5", false),
                (ChordTypeAdditions.Flat6, "b6", false),
                (ChordTypeAdditions.Flat9, "b9", true),
                (ChordTypeAdditions.Sharp9, "#9", true),
                (ChordTypeAdditions.Flat11, "b11", true),
                (ChordTypeAdditions.Sharp11, "#11", true),
                (ChordTypeAdditions.Flat13, "b13", true),
                (ChordTypeAdditions.Sharp13, "#13", true),
            }
            .Where(x => additions.HasFlag(x.Item1))
            .ToList();

        if (hasSeventh)
        {
            result += string.Concat(alterations.Select(x => x.Item2));
        }
        else
        {
            // Without a seventh, the altered fifth and sixth go into parentheses, so that "C(b5)" is not read as the root Cb,
            // and the altered upper degrees are added, so that "Caddb9" is not read as C7(b9).
            var inParentheses = alterations.Where(x => !x.Item3).Select(x => x.Item2).ToList();
            if (inParentheses.Any())
                result += $"({string.Join(",", inParentheses)})";

            result += string.Concat(alterations.Where(x => x.Item3).Select(x => "add" + x.Item2));
        }

        foreach (var (addition, name) in new[]
                 {
                     (ChordTypeAdditions.No5, "no5"),
                     (ChordTypeAdditions.No7, "no7"),
                     (ChordTypeAdditions.No9, "no9"),
                     (ChordTypeAdditions.No11, "no11"),
                 })
        {
            if (additions.HasFlag(addition))
                result += name;
        }

        return result;
    }
}
