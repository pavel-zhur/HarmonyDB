using HarmonyDB.Theory.Chords.Models.Enums;

namespace HarmonyDB.Theory.Chords.Models;

public record ChordParseResult
{
    public ChordParseResult(ChordParseResultType type)
    {
        if (type != ChordParseResultType.SpecialNoChord)
            throw new ArgumentOutOfRangeException(nameof(type), type, "Use a different constructor.");

        Type = type;
        Trace = new();
    }

    public ChordParseResult(ChordRepresentation chord, ChordParseTrace trace)
    {
        Type = ChordParseResultType.Success;
        Chord = chord;
        Trace = trace;
    }

    public ChordParseResult(ChordParseError error, ChordParseTrace trace)
    {
        Type = ChordParseResultType.Error;
        Error = error;
        Trace = trace;
    }

    public ChordParseResultType Type { get; }
    public ChordRepresentation? Chord { get; }
    public ChordParseError? Error { get; }
    public ChordParseTrace Trace { get; }
}