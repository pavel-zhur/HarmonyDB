using HarmonyDB.Index.Analysis.Em.Models;

namespace HarmonyDB.Index.Analysis.Tests.Em;

public class TestLoop : ILoop
{
    public required string Id { get; init; }
    public float[,] TonalityProbabilities { get; set; } = new float[Constants.TonicCount, Constants.ScaleCount];
    public (float TonicScore, float ScaleScore) Score { get; set; }
    public required (byte Tonic, Scale Scale)[] SecretTonalities { get; init; }
}