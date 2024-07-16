namespace HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

public record Loop(

    // StructureLoop mirror
    string Normalized,
    int Length,
    float TotalOccurrences,
    float TotalSuccessions,
    float AverageCoverage,
    int TotalSongs,

    // new
    float[] Probabilities,
    float ScaleScore,
    float TonicScore);