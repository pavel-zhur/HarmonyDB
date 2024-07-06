namespace HarmonyDB.Index.Analysis.Tests.AiGeneratedKeysDetection;

public class Loop : ILoop
{
    public string Id { get; set; }
    public double[] TonalityProbabilities { get; set; } = new double[Constants.TonalityCount];
    public double Score { get; set; } = 1.0;
    public int SongCount { get; set; } = 0;
    public int[] SecretTonalities { get; set; }
}