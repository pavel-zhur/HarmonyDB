using HarmonyDB.Theory.Chords.Models.Enums;

namespace HarmonyDB.Theory.Chords.Models;

public readonly record struct ChordTypeToken
{
    public ChordTypeToken(ChordType? Type)
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

    public ChordTypeToken(byte? fret)
    {
        Fret = fret;
    }

    public ChordTypeToken(ChordTypeMeaninglessAddition? meaninglessAddition)
    {
        MeaninglessAddition = meaninglessAddition;
    }

    public ChordTypeToken(ChordTypeAmbiguousAddition? ambiguousAddition)
    {
        AmbiguousAddition = ambiguousAddition;
    }

    public ChordType? Type { get; }
    public ChordTypeExtension? Extension { get; }
    public ChordTypeAdditions? Addition { get; }
    public byte? Fret { get; }
    public ChordTypeMeaninglessAddition? MeaninglessAddition { get; }
    public ChordTypeAmbiguousAddition? AmbiguousAddition { get; }

    public override string ToString()
        => Type?.ToString()
           ?? Extension?.ToString()
           ?? Addition?.ToString()
           ?? Fret?.ToString()
           ?? MeaninglessAddition?.ToString()
           ?? AmbiguousAddition.ToString()!;
}