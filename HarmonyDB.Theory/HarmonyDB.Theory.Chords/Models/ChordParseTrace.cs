using HarmonyDB.Theory.Chords.Models.Enums;

namespace HarmonyDB.Theory.Chords.Models;

public record ChordParseTrace
{
    public string? ChordTypeRepresentation { get; set; }
    public List<(ChordTypeToken token, bool fromParentheses, MatchAmbiguity matchAmbiguity)>? ChordTypeTokens { get; set; }
    public ChordTypeParseLogic? ChordTypeParseLogic { get; set; }
    public byte? ChordTypeParseBranchIndex { get; set; }
}