using HarmonyDB.Index.Analysis.Models.Index;

namespace HarmonyDB.Index.Api.Model.VExternal1;

public record StructureLoopTonality(

    // base
    string Normalized,
    int Length,
    float TotalOccurrences,
    float TotalSuccessions,
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
        TotalSongs);