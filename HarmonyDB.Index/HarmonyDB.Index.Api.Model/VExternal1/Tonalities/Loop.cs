using HarmonyDB.Index.Api.Model.VExternal1.Caches;

namespace HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

public record Loop(

    // base
    string Normalized,
    int Length,
    float TotalOccurrences,
    float TotalSuccessions,
    float AverageCoverage,
    int TotalSongs,

    // new
    float[] Probabilities,
    float ScaleScore,
    float TonicScore)

    : StructureLoop(
        Normalized,
        Length, 
        TotalOccurrences,
        TotalSuccessions,
        AverageCoverage,
        TotalSongs);