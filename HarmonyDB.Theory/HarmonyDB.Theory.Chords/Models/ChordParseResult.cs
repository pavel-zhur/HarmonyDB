using HarmonyDB.Theory.Chords.Models.Enums;

namespace HarmonyDB.Theory.Chords.Models;

public record ChordParseResult
{
    public ChordParseResult(ChordParseResultType resultType)
    {
        if (resultType == ChordParseResultType.Success)
            throw new ArgumentOutOfRangeException(nameof(resultType), resultType, "Use a different constructor.");

        ResultType = resultType;
    }

    public ChordParseResult(ChordRepresentation chord)
    {
        ResultType = ChordParseResultType.Success;
        Chord = chord;
    }

    public ChordParseResult(ChordParseResultError error)
    {
        ResultType = ChordParseResultType.Error;
        Error = error;
    }

    public ChordParseResultType ResultType { get; }
    public ChordRepresentation? Chord { get; }
    public ChordParseResultError? Error { get; }
}