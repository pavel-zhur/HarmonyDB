namespace HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

public record LinkStatistics(
    int DerivedTonalityIndex,
    bool DerivedFromKnown,
    int Count, 
    float TotalWeight,
    float Occurrences,
    float Successions, 
    float AverageCoverage,
    float AverageTonicScore,
    float AverageScaleScore,
    float AverageConfidence,
    List<LinkExample> Examples);