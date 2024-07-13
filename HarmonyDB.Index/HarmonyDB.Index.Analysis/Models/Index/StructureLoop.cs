namespace HarmonyDB.Index.Analysis.Models.Index;

public record StructureLoop(string Normalized, int Length, float TotalOccurrences, float TotalSuccessions, float AverageCoverage, int TotalSongs);