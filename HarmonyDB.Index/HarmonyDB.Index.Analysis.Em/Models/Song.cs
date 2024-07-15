namespace HarmonyDB.Index.Analysis.Em.Models;

public class Song : ISource
{
    public required string Id { get; init; }
    public float[,] TonalityProbabilities { get; set; } = new float[Constants.TonicCount, Constants.ScaleCount];
    public (float TonicScore, float ScaleScore) Score { get; set; }
    public required (byte Tonic, Scale Scale)? KnownTonality { get; init; }
}