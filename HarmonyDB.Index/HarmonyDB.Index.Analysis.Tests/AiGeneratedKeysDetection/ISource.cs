namespace HarmonyDB.Index.Analysis.Tests.AiGeneratedKeysDetection;

public interface ISource
{
    string Id { get; set; }
    double[] TonalityProbabilities { get; set; }
    double Score { get; set; }
}