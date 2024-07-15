using HarmonyDB.Index.Analysis.Em.Models;

namespace HarmonyDB.Index.Analysis.Tests.Em;

public class TestSong : Song
{
    public required (byte Tonic, Scale Scale)[] SecretTonalities { get; init; }
    public required bool IsKnownTonalityIncorrect { get; init; }
}