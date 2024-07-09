using HarmonyDB.Index.Analysis.Em.Models;

namespace HarmonyDB.Index.Analysis.Models.Em;

public class Loop : ISource, ILoop
{
    public required string Id { get; set; }
    public double[,] TonalityProbabilities { get; set; } = new double[Constants.TonicCount, Constants.ScaleCount];
    public (double TonicScore, double ScaleScore) Score { get; set; }
    public required int Length { get; init; }
}