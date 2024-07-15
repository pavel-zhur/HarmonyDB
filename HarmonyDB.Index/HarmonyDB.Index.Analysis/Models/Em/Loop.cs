using HarmonyDB.Index.Analysis.Em.Models;

namespace HarmonyDB.Index.Analysis.Models.Em;

public class Loop : ISource, ILoop
{
    public required string Id { get; init; }
    public float[,] TonalityProbabilities { get; set; } = new float[Constants.TonicCount, Constants.ScaleCount];
    public (float TonicScore, float ScaleScore) Score { get; set; }
}