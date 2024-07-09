namespace HarmonyDB.Index.Analysis.Models.Index;

public record Link(string Normalized, string ExternalId, byte NormalizationRoot, short Occurrences, short Successions);