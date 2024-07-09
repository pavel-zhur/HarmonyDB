namespace HarmonyDB.Index.Analysis.Em.Models;

public interface ISource
{
    string Id { get; }
    float[,] TonalityProbabilities { get; set; } // [TonicCount, ScaleCount]
    (float TonicScore, float ScaleScore) Score { get; set; }
}