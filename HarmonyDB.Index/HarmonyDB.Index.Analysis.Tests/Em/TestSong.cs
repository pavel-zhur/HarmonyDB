using HarmonyDB.Index.Analysis.Em.Models;

namespace HarmonyDB.Index.Analysis.Tests.Em;

public class TestSong : ISong
{
    public required string Id { get; init; }
    public float[,] TonalityProbabilities { get; set; } = new float[Constants.TonicCount, Constants.ScaleCount];
    public (float TonicScore, float ScaleScore) Score { get; set; }
    public required bool IsTonalityKnown { get; init; }
    public required (int Tonic, Scale Scale) KnownTonality { get; init; }
    public required (int Tonic, Scale Scale)[] SecretTonalities { get; init; }
    public required bool IsKnownTonalityIncorrect { get; init; }
}