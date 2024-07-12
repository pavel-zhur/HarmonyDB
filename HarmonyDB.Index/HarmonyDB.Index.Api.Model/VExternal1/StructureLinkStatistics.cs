namespace HarmonyDB.Index.Api.Model.VExternal1;

public record StructureLinkStatistics(
    int DerivedTonalityIndex,
    bool DerivedFromKnown,
    int Count, 
    float TotalWeight,
    int Occurrences, 
    int Successions, 
    float AverageTonicScore,
    float AverageScaleScore,
    float AverageConfidence,
    List<StructureLinkExample> Examples);