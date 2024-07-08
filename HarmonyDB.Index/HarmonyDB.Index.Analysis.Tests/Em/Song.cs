using HarmonyDB.Index.Analysis.Em.Models;

namespace HarmonyDB.Index.Analysis.Tests.Em;

public class Song : ISong
{
    public string Id { get; set; }
    public double[,] TonalityProbabilities { get; set; } = new double[Constants.TonicCount, Constants.ScaleCount];
    public (double TonicScore, double ScaleScore) Score { get; set; }
    public bool IsTonalityKnown { get; set; }
    public (int Tonic, Scale Scale) KnownTonality { get; set; }
    public (int Tonic, Scale Scale)[] SecretTonalities { get; set; }
    public bool IsKnownTonalityIncorrect { get; set; }
}