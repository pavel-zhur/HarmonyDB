namespace HarmonyDB.Index.Analysis.Em.Models;

public interface ISource
{
    string Id { get; }
    double[,] TonalityProbabilities { get; set; } // [TonicCount, ScaleCount]
    (double TonicScore, double ScaleScore) Score { get; set; }
}