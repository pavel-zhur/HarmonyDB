namespace HarmonyDB.Index.Api.Model.VExternal1;

public record StructureLinkStatistics(int? KnownTonalityIndex, int PredictedTonalityIndex, byte NormalizationRoot, int Count, float TotalWeight, int Occurrences, int Successions, List<StructureSongTonality> Examples);