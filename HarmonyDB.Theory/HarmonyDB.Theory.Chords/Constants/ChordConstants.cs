using HarmonyDB.Theory.Chords.Models;
using HarmonyDB.Theory.Chords.Models.Enums;
using OneShelf.Common;

namespace HarmonyDB.Theory.Chords.Constants;

public static class ChordConstants
{
    public const string NoChord = "N.C.";

    public static readonly IReadOnlyList<string> NoChordVariants =
    [
        NoChord,
        "N.C",
        "NC",
        "N.С.", // russian C
    ];

    public static readonly IReadOnlyList<(ChordMainType type, string representation, MatchCase matchCase, MatchAmbiguity matchAmbiguity)> ChordMainTypeRepresentations =
    [
        (ChordMainType.Major, "major", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),

        (ChordMainType.Minor, "m", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordMainType.Minor, "mi", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordMainType.Minor, "min", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordMainType.Minor, "minor", MatchCase.MatchAny, MatchAmbiguity.Safe),

        (ChordMainType.Dim, "dim", MatchCase.MatchAny, MatchAmbiguity.Safe),

        (ChordMainType.Aug, "aug", MatchCase.MatchAny, MatchAmbiguity.Safe),

        (ChordMainType.Dim7, "dim7", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordMainType.Dim7, "º", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordMainType.Dim7, "°", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordMainType.Dim7, "o", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordMainType.Dim7, "º7", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordMainType.Dim7, "°7", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordMainType.Dim7, "o7", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordMainType.Sus2, "sus2", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordMainType.Sus2, "s2", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordMainType.Sus2, "susp2", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordMainType.Sus4, "sus4", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordMainType.Sus4, "s4", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordMainType.Sus4, "susp4", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordMainType.Sus4, "sus", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordMainType.Sus4, "susp", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordMainType.Power, "5", MatchCase.ExactOnly, MatchAmbiguity.AlterableInt),
        (ChordMainType.Power, "no3", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordMainType.Power, "no3rd", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordMainType.Power, "omit3", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordMainType.Power, "oMIt3", MatchCase.ExactOnly, MatchAmbiguity.Safe),
    ];

    public static readonly IReadOnlyList<(ChordTypeExtension extension, string representation, MatchCase matchCase, MatchAmbiguity matchAmbiguity)> ChordTypeExtensionRepresentations =
    [
        (ChordTypeExtension.X7, "add7", MatchCase.MatchAny, MatchAmbiguity.AlterableDegree),
        (ChordTypeExtension.X7, "7", MatchCase.ExactOnly, MatchAmbiguity.AlterableInt),
        (ChordTypeExtension.X9, "9", MatchCase.ExactOnly, MatchAmbiguity.AlterableInt),
        (ChordTypeExtension.X11, "11", MatchCase.ExactOnly, MatchAmbiguity.AlterableInt),
        (ChordTypeExtension.X13, "13", MatchCase.ExactOnly, MatchAmbiguity.AlterableInt),

        (ChordTypeExtension.XMaj7, "maj7", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "maj", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "ma7", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "major7", MatchCase.MatchUpperFirst, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "M7", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "MA7", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "M", MatchCase.ExactOnly, MatchAmbiguity.Dangerous),
        (ChordTypeExtension.XMaj7, "7M", MatchCase.ExactOnly, MatchAmbiguity.Dangerous),
        (ChordTypeExtension.XMaj7, "Δ", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "∆", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "△", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "^", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "Δ7", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "∆7", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "△7", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "^7", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordTypeExtension.XMaj9, "maj9", MatchCase.MatchAny, MatchAmbiguity.AlterableDegree),
        (ChordTypeExtension.XMaj9, "ma9", MatchCase.ExactOnly, MatchAmbiguity.AlterableDegree),
        (ChordTypeExtension.XMaj9, "M9", MatchCase.ExactOnly, MatchAmbiguity.AlterableDegree),

        (ChordTypeExtension.XMaj11, "maj11", MatchCase.MatchAny, MatchAmbiguity.AlterableDegree),
        (ChordTypeExtension.XMaj11, "M11", MatchCase.ExactOnly, MatchAmbiguity.AlterableDegree),

        (ChordTypeExtension.XMaj13, "maj13", MatchCase.MatchAny, MatchAmbiguity.AlterableDegree),
        (ChordTypeExtension.XMaj13, "M13", MatchCase.ExactOnly, MatchAmbiguity.AlterableDegree),
    ];

    public static readonly IReadOnlyList<(ChordTypeAdditions addition, string representation, MatchCase matchCase, MatchAmbiguity matchAmbiguity)> ChordTypeAdditionRepresentations =
    [
        (ChordTypeAdditions.No5, "no5", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.No5, "no5th", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.No5, "omit5", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.No5, "oMIt5", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordTypeAdditions.No7, "no7", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordTypeAdditions.No9, "no9", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordTypeAdditions.No11, "no11", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordTypeAdditions.Add2, "2", MatchCase.ExactOnly, MatchAmbiguity.AlterableInt),
        (ChordTypeAdditions.Add2, "add2", MatchCase.MatchAny, MatchAmbiguity.AlterableDegree),

        (ChordTypeAdditions.Add4, "4", MatchCase.ExactOnly, MatchAmbiguity.AlterableInt),
        (ChordTypeAdditions.Add4, "add4", MatchCase.MatchAny, MatchAmbiguity.AlterableDegree),

        (ChordTypeAdditions.Add6, "6", MatchCase.ExactOnly, MatchAmbiguity.AlterableInt),
        (ChordTypeAdditions.Add6, "add6", MatchCase.MatchAny, MatchAmbiguity.AlterableDegree),

        (ChordTypeAdditions.Flat2, "addb2", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat5, "addb5", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat6, "addb6", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat9, "addb9", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat11, "addb11", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat13, "addb13", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat2, "add-2", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat5, "add-5", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat6, "add-6", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat9, "add-9", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat11, "add-11", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat13, "add-13", MatchCase.MatchAny, MatchAmbiguity.Safe),
        
        (ChordTypeAdditions.Sharp4, "add#4", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp5, "add#5", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp9, "add#9", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp11, "add#11", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp13, "add#13", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp4, "add+4", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp5, "add+5", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp9, "add+9", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp11, "add+11", MatchCase.MatchAny, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp13, "add+13", MatchCase.MatchAny, MatchAmbiguity.Safe),

        (ChordTypeAdditions.Add9, "add9", MatchCase.MatchAny, MatchAmbiguity.AlterableDegree),
        (ChordTypeAdditions.Add11, "add11", MatchCase.MatchAny, MatchAmbiguity.AlterableDegree),
        (ChordTypeAdditions.Add13, "add13", MatchCase.MatchAny, MatchAmbiguity.AlterableDegree),
    ];

    public static readonly IReadOnlyList<(ChordTypeAmbiguousAddition addition, string representation, MatchCase matchCase, MatchAmbiguity matchAmbiguity)> ChordTypeAmbiguousAdditionRepresentations =
    [
        (ChordTypeAmbiguousAddition.HalfDiminished7, "ø", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAmbiguousAddition.HalfDiminished7, "Ø", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAmbiguousAddition.HalfDiminished7, "ø7", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAmbiguousAddition.HalfDiminished7, "Ø7", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordTypeAmbiguousAddition.Sharp, "#", MatchCase.ExactOnly, MatchAmbiguity.DegreeAlteration),
        (ChordTypeAmbiguousAddition.Sharp, "♯", MatchCase.ExactOnly, MatchAmbiguity.DegreeAlteration),
        (ChordTypeAmbiguousAddition.Flat, "b", MatchCase.ExactOnly, MatchAmbiguity.DegreeAlteration),
        (ChordTypeAmbiguousAddition.Flat, "♭", MatchCase.ExactOnly, MatchAmbiguity.DegreeAlteration),
        (ChordTypeAmbiguousAddition.Plus, "+", MatchCase.ExactOnly, MatchAmbiguity.DegreeAlteration), // generally means augmented if at the beginning and no degree follows
        (ChordTypeAmbiguousAddition.Minus, "-", MatchCase.ExactOnly, MatchAmbiguity.DegreeAlteration), // generally means minor if at the beginning and no degree follows

        (ChordTypeAmbiguousAddition.Add9And11, "add9,11", MatchCase.MatchAny, MatchAmbiguity.AlterableDegree),
    ];

    public static readonly IReadOnlyList<(ChordTypeMeaninglessAddition addition, string representation)> ChordTypeMeaninglessAdditionRepresentations =
    [
        (ChordTypeMeaninglessAddition.Star, "*"),
        (ChordTypeMeaninglessAddition.Question, "?"),
        (ChordTypeMeaninglessAddition.Slash, "/"),
        (ChordTypeMeaninglessAddition.Slash, "\\"),
        (ChordTypeMeaninglessAddition.Apostrophe, "'"),
    ];

    public static readonly IReadOnlyList<string> Romans =
    [
        "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X",
        "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX",
    ];

    public static readonly IReadOnlyList<(ChordTypeToken token, string representation, MatchCase matchCase, MatchAmbiguity matchAmbiguity)> AllRepresentations
        = ChordMainTypeRepresentations
            .Select(x => (new ChordTypeToken(x.type), x.representation, x.matchCase, x.matchAmbiguity))
            .Concat(ChordTypeExtensionRepresentations
                .Select(x => (new ChordTypeToken(x.extension), x.representation, x.matchCase, x.matchAmbiguity)))
            .Concat(ChordTypeAdditionRepresentations
                .Select(x => (new ChordTypeToken(x.addition), x.representation, x.matchCase, x.matchAmbiguity)))
            .Concat(ChordTypeMeaninglessAdditionRepresentations
                .Select(x => (new ChordTypeToken(x.addition), x.representation, MatchCase.ExactOnly, MatchAmbiguity.Safe)))
            .Concat(ChordTypeAmbiguousAdditionRepresentations
                .Select(x => (new ChordTypeToken(x.addition), x.representation, x.matchCase, x.matchAmbiguity)))
            .OrderByDescending(x => x.representation.Length)
            .ToList();

    public static readonly IReadOnlyDictionary<ChordTypeToken, string> CanonicalRepresentations
        = ChordMainTypeRepresentations
            .Select(x => (token: new ChordTypeToken(x.type), x.representation))
            .Concat(ChordTypeExtensionRepresentations
                .Select(x => (token: new ChordTypeToken(x.extension), x.representation)))
            .Concat(ChordTypeAdditionRepresentations
                .Select(x => (token: new ChordTypeToken(x.addition), x.representation)))
            .Concat(ChordTypeMeaninglessAdditionRepresentations
                .Select(x => (token: new ChordTypeToken(x.addition), x.representation)))
            .Concat(ChordTypeAmbiguousAdditionRepresentations
                .Select(x => (token: new ChordTypeToken(x.addition), x.representation)))
            .Prepend((new(ChordMainType.Major), string.Empty))
            .GroupBy(x => x.token)
            .ToDictionary(g => g.Key, g => g.First().representation);
}