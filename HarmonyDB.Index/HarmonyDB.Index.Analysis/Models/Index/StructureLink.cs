namespace HarmonyDB.Index.Analysis.Models.Index;

public record StructureLink(string Normalized, string ExternalId, byte NormalizationRoot, short Occurrences, short Successions);