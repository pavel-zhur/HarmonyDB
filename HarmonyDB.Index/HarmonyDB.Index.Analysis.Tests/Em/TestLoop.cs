using HarmonyDB.Index.Analysis.Em.Models;

namespace HarmonyDB.Index.Analysis.Tests.Em;

public class TestLoop : Loop
{
    public required (byte Tonic, Scale Scale)[] SecretTonalities { get; init; }
}