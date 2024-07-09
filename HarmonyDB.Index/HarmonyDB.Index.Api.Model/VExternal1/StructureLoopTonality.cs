using HarmonyDB.Index.Analysis.Models.Index;

namespace HarmonyDB.Index.Api.Model.VExternal1;

public record StructureLoopTonality(
    string Normalized,
    int Length,
    int TotalOccurrences,
    int TotalSuccessions,
    int TotalSongs,
    string Title,
    float[] Probabilities,
    float ScaleScore,
    float TonicScore)
    : StructureLoop(Normalized,
        Length, 
        TotalOccurrences,
        TotalSuccessions,
        TotalSongs,
        Title);