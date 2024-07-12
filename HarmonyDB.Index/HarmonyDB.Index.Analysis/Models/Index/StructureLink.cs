namespace HarmonyDB.Index.Analysis.Models.Index;

public record StructureLink(string Normalized, string ExternalId, byte NormalizationRoot, float Occurrences, float Successions, float Coverage);