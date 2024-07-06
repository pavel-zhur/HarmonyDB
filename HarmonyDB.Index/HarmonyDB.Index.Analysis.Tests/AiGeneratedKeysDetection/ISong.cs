namespace HarmonyDB.Index.Analysis.Tests.AiGeneratedKeysDetection;

public interface ISong : ISource
{
    bool IsTonalityKnown { get; set; }
    int KnownTonality { get; set; }
}