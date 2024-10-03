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

    public static readonly IReadOnlyList<(ChordType type, string representation, MatchCase matchCase, MatchAmbiguity matchAmbiguity)> ChordTypeRepresentations =
    [
        (ChordType.Major, "major", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),

        (ChordType.Minor, "m", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Minor, "min", MatchCase.MatchUpperFirst, MatchAmbiguity.Safe),
        (ChordType.Minor, "minor", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),

        (ChordType.Dim, "dim", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),

        (ChordType.Aug, "aug", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),

        (ChordType.Dim7, "dim7", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordType.Dim7, "º", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Dim7, "°", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Dim7, "o", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordType.Sus2, "sus2", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordType.Sus2, "susp2", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordType.Sus4, "sus4", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordType.Sus4, "susp4", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Sus4, "sus", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordType.Sus4, "susp", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordType.Power, "5", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Power, "no3", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Power, "no3rd", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Power, "omit3", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordType.Power, "oMIt3", MatchCase.ExactOnly, MatchAmbiguity.Safe),
    ];

    public static readonly IReadOnlyList<(ChordTypeExtension extension, string representation, MatchCase matchCase, MatchAmbiguity matchAmbiguity)> ChordTypeExtensionRepresentations =
    [
        (ChordTypeExtension.X7, "7", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.X9, "9", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.X11, "11", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.X13, "13", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordTypeExtension.XMaj7, "maj", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "maj7", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "major7", MatchCase.MatchUpperFirst, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "M7", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeExtension.XMaj7, "M", MatchCase.ExactOnly, MatchAmbiguity.Dangerous),
        (ChordTypeExtension.XMaj7, "7M", MatchCase.ExactOnly, MatchAmbiguity.Dangerous),

        (ChordTypeExtension.XMaj9, "maj9", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
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

        (ChordTypeAdditions.Add2, "2", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Add2, "add2", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),

        (ChordTypeAdditions.Add4, "4", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Add4, "add4", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        
        (ChordTypeAdditions.Flat5, "addb5", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat6, "addb6", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat9, "addb9", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat11, "addb11", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Flat13, "addb13", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        
        (ChordTypeAdditions.Sharp4, "add#4", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp5, "add#5", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp9, "add#9", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp11, "add#11", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Sharp13, "add#13", MatchCase.ExactOnly, MatchAmbiguity.Safe),

        (ChordTypeAdditions.Add6, "6", MatchCase.ExactOnly, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Add6, "add6", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),

        (ChordTypeAdditions.Add9, "add9", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Add11, "add11", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),
        (ChordTypeAdditions.Add13, "add13", MatchCase.MatchUpperFirstOrAll, MatchAmbiguity.Safe),

        (ChordTypeAdditions.HalfDiminished7, "ø", MatchCase.ExactOnly, MatchAmbiguity.Safe),
    ];

    public static readonly IReadOnlyList<(ChordTypeMeaninglessAddition meaninglessAddition, string representation)> ChordTypeMeaninglessAdditionRepresentations =
    [
        (ChordTypeMeaninglessAddition.Star, "*"),
        (ChordTypeMeaninglessAddition.Question, "?"),
    ];

    public static readonly IReadOnlyList<string> Romans =
    [
        "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X",
        "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX",
    ];

    public static readonly IReadOnlyList<(ChordType? type, ChordTypeExtension? extension, ChordTypeAdditions? addition, byte? fret, ChordTypeMeaninglessAddition? meaninglessAddition, string representation, MatchCase matchCase, MatchAmbiguity matchAmbiguity)> AllRepresentations
        = ChordTypeRepresentations
            .Select(x => ((ChordType?)x.type, (ChordTypeExtension?)null, (ChordTypeAdditions?)null, (byte?)null, (ChordTypeMeaninglessAddition?)null, x.representation, x.matchCase, x.matchAmbiguity))
            .Concat(ChordTypeExtensionRepresentations
                .Select(x => ((ChordType?)null, (ChordTypeExtension?)x.extension, (ChordTypeAdditions?)null, (byte?)null, (ChordTypeMeaninglessAddition?)null, x.representation, x.matchCase, x.matchAmbiguity)))
            .Concat(ChordTypeAdditionRepresentations
                .Select(x => ((ChordType?)null, (ChordTypeExtension?)null, (ChordTypeAdditions?)x.addition, (byte?)null, (ChordTypeMeaninglessAddition?)null, x.representation, x.matchCase, x.matchAmbiguity)))
            .Concat(Romans.WithIndices()
                .Select(x => ((ChordType?)null, (ChordTypeExtension?)null, (ChordTypeAdditions?)null, (byte?)(x.i + 1), (ChordTypeMeaninglessAddition?)null, representation: x.x, MatchCase.ExactOnly, Definite: MatchAmbiguity.Safe)))
            .Concat(ChordTypeMeaninglessAdditionRepresentations
                .Select(x => ((ChordType?)null, (ChordTypeExtension?)null, (ChordTypeAdditions?)null, (byte?)null, (ChordTypeMeaninglessAddition?)x.meaninglessAddition, representation: x.representation, MatchCase.ExactOnly, Definite: MatchAmbiguity.Safe)))
            .OrderByDescending(x => x.representation.Length)
            .ToList();
}