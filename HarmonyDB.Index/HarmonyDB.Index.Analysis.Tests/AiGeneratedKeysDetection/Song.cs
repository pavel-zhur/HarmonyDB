namespace HarmonyDB.Index.Analysis.Tests.AiGeneratedKeysDetection;

public class Song : ISong
{
    public required string Id { get; set; }
    public double[] TonalityProbabilities { get; set; } = new double[Constants.TonalityCount];
    public double Score { get; set; } = 1.0;
    public bool IsTonalityKnown { get; set; }
    public int KnownTonality { get; set; }
    public required int[] SecretTonalities { get; init; }
    public bool IsKnownTonalityIncorrect { get; init; }
}