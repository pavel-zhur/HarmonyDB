using HarmonyDB.Theory.Chords.Models.Enums;
using HarmonyDB.Theory.Chords.Models.Internal;
using HarmonyDB.Theory.Chords.Models.Internal.Enums;
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

    public static readonly IReadOnlyList<(ChordType type, string representation, MatchCase matchCase, MatchAmbiguity matchAmbiguity)> ChordTypeRepresentations =
    [
        (ChordType.Major, "major", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),

        (ChordType.Minor, "m", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Minor, "mi", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Minor, "min", MatchCase.MatchUpperFirst, MatchAmbiguity.Safe),
        (ChordType.Minor, "minor", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),

        (ChordType.Dim, "dim", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),

        (ChordType.Aug, "aug", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),

        (ChordType.Dim7, "dim7", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordType.Dim7, "º", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Dim7, "°", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Dim7, "o", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordType.Sus2, "sus2", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordType.Sus2, "s2", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Sus2, "susp2", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordType.Sus4, "sus4", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordType.Sus4, "s4", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Sus4, "susp4", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Sus4, "sus", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordType.Sus4, "susp", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordType.Power, "5", MatchCase.ExactOnly, MatchAmbiguity.Degree),
        (ChordType.Power, "no3", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Power, "no3rd", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Power, "omit3", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Power, "oMIt3", MatchCase.ExactOnly, MatchAmbiguity.Safe),
    ];

    public static readonly IReadOnlyList<(ChordTypeExtension extension, string representation, MatchCase matchCase, MatchAmbiguity matchAmbiguity)> ChordTypeExtensionRepresentations =
    [
        (ChordTypeExtension.X7, "add7", MatchCase.ExactOnly, MatchAmbiguity.Degree),
        (ChordTypeExtension.X7, "7", MatchCase.ExactOnly, MatchAmbiguity.Degree),
        (ChordTypeExtension.X9, "9", MatchCase.ExactOnly, MatchAmbiguity.Degree),
        (ChordTypeExtension.X11, "11", MatchCase.ExactOnly, MatchAmbiguity.Degree),
        (ChordTypeExtension.X13, "13", MatchCase.ExactOnly, MatchAmbiguity.Degree),

        (ChordTypeExtension.XMaj7, "maj7", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "maj", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "ma7", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "major7", MatchCase.MatchUpperFirst, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "M7", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "MA7", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "M", MatchCase.ExactOnly, MatchAmbiguity.Dangerous),
        (ChordTypeExtension.XMaj7, "7M", MatchCase.ExactOnly, MatchAmbiguity.Dangerous),

        (ChordTypeExtension.XMaj9, "maj9", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj9, "ma9", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj9, "M9", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordTypeExtension.XMaj11, "maj11", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj11, "M11", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordTypeExtension.XMaj13, "maj13", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj13, "M13", MatchCase.ExactOnly, MatchAmbiguity.Safe),
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

        (ChordTypeAdditions.Add2, "2", MatchCase.ExactOnly, MatchAmbiguity.Degree),
        (ChordTypeAdditions.Add2, "add2", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Degree),

        (ChordTypeAdditions.Add4, "4", MatchCase.ExactOnly, MatchAmbiguity.Degree),
        (ChordTypeAdditions.Add4, "add4", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Degree),
        
        (ChordTypeAdditions.Flat5, "addb5", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat6, "addb6", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat9, "addb9", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat11, "addb11", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat13, "addb13", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat5, "add-5", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat6, "add-6", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat9, "add-9", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat11, "add-11", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat13, "add-13", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        
        (ChordTypeAdditions.Sharp4, "add#4", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp5, "add#5", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp9, "add#9", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp11, "add#11", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp13, "add#13", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp4, "add+4", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp5, "add+5", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp9, "add+9", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp11, "add+11", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp13, "add+13", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordTypeAdditions.Add6, "6", MatchCase.ExactOnly, MatchAmbiguity.Degree),
        (ChordTypeAdditions.Add6, "add6", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Degree),

        (ChordTypeAdditions.Add9, "add9", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Degree),
        (ChordTypeAdditions.Add11, "add11", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Degree),
        (ChordTypeAdditions.Add13, "add13", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Degree),
    ];

    internal static readonly IReadOnlyList<(ChordTypeAmbiguousAddition addition, string representation, MatchCase matchCase, MatchAmbiguity matchAmbiguity)> ChordTypeAmbiguousAdditionRepresentations =
    [
        (ChordTypeAmbiguousAddition.HalfDiminished7, "ø", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordTypeAmbiguousAddition.Sharp, "#", MatchCase.ExactOnly, MatchAmbiguity.DegreeAlteration),
        (ChordTypeAmbiguousAddition.Flat, "b", MatchCase.ExactOnly, MatchAmbiguity.DegreeAlteration),
        (ChordTypeAmbiguousAddition.Plus, "+", MatchCase.ExactOnly, MatchAmbiguity.DegreeAlteration),
        (ChordTypeAmbiguousAddition.Minus, "-", MatchCase.ExactOnly, MatchAmbiguity.DegreeAlteration),
    ];

    internal static readonly IReadOnlyList<(ChordTypeMeaninglessAddition addition, string representation)> ChordTypeMeaninglessAdditionRepresentations =
    [
        (ChordTypeMeaninglessAddition.Star, "*"),
        (ChordTypeMeaninglessAddition.Question, "?"),
        (ChordTypeMeaninglessAddition.Slash, "/"),
    ];

    public static readonly IReadOnlyList<string> Romans =
    [
        "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X",
        "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX",
    ];

    internal static readonly IReadOnlyList<(ChordTypeToken token, string representation, MatchCase matchCase, MatchAmbiguity matchAmbiguity)> AllRepresentations
        = ChordTypeRepresentations
            .Select(x => (new ChordTypeToken(x.type), x.representation, x.matchCase, x.matchAmbiguity))
            .Concat(ChordTypeExtensionRepresentations
                .Select(x => (new ChordTypeToken(x.extension), x.representation, x.matchCase, x.matchAmbiguity)))
            .Concat(ChordTypeAdditionRepresentations
                .Select(x => (new ChordTypeToken(x.addition), x.representation, x.matchCase, x.matchAmbiguity)))
            .Concat(Romans.WithIndices()
                .Select(x => (new ChordTypeToken((byte)(x.i + 1)), representation: x.x, MatchCase.ExactOnly, Definite: MatchAmbiguity.Safe)))
            .Concat(ChordTypeMeaninglessAdditionRepresentations
                .Select(x => (new ChordTypeToken(x.addition), x.representation, MatchCase.ExactOnly, Definite: MatchAmbiguity.Safe)))
            .Concat(ChordTypeAmbiguousAdditionRepresentations
                .Select(x => (new ChordTypeToken(x.addition), x.representation, MatchCase.ExactOnly, Definite: MatchAmbiguity.Safe)))
            .OrderByDescending(x => x.representation.Length)
            .ToList();
}