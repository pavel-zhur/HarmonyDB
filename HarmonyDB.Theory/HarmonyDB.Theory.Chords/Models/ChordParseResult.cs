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

    public ChordParseResult(Chord chord)
    {
        ResultType = ChordParseResultType.Success;
        Chord = chord;
    }

    public ChordParseResultType ResultType { get; }
    public Chord? Chord { get; }
}