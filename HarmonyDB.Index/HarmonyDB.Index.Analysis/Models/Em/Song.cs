using HarmonyDB.Index.Analysis.Em.Models;

namespace HarmonyDB.Index.Analysis.Models.Em;

public class Song : ISource, ISong
{
    public required string Id { get; set; }
    public float[,] TonalityProbabilities { get; set; } = new float[Constants.TonicCount, Constants.ScaleCount];
    public (float TonicScore, float ScaleScore) Score { get; set; }
    public required bool IsTonalityKnown { get; set; }
    public required (int Tonic, Scale Scale) KnownTonality { get; set; }
}