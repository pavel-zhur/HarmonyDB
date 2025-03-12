using HarmonyDB.Theory.Chords.Constants;
using HarmonyDB.Theory.Chords.Models.Enums;

namespace HarmonyDB.Theory.Chords.Models;

public readonly record struct ChordTypeToken
{
    public ChordTypeToken(ChordMainType? Type)
    {
        this.Type = Type;
    }

    public ChordTypeToken(ChordTypeExtension? extension)
    {
        Extension = extension;
    }

    public ChordTypeToken(ChordTypeAdditions? addition)
    {
        Addition = addition;
    }

    public ChordTypeToken(ChordTypeMeaninglessAddition? meaninglessAddition)
    {
        MeaninglessAddition = meaninglessAddition;
    }

    public ChordTypeToken(ChordTypeAmbiguousAddition? ambiguousAddition)
    {
        AmbiguousAddition = ambiguousAddition;
    }

    public ChordMainType? Type { get; }
    public ChordTypeExtension? Extension { get; }
    public ChordTypeAdditions? Addition { get; }
    public ChordTypeMeaninglessAddition? MeaninglessAddition { get; }
    public ChordTypeAmbiguousAddition? AmbiguousAddition { get; }

    public override string ToString()
        => Type?.ToString()
           ?? Extension?.ToString()
           ?? Addition?.ToString()
           ?? MeaninglessAddition?.ToString()
           ?? AmbiguousAddition.ToString()!;

    public string ToCanonical()
    {
        return ChordConstants.CanonicalRepresentations[this];
    }
}