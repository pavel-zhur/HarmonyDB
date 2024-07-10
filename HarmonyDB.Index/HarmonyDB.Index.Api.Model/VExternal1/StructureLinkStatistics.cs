namespace HarmonyDB.Index.Api.Model.VExternal1;

public record StructureLinkStatistics(int? KnownTonality, int PredictedTonality, int Count, float TotalWeight, byte NormalizationRoot, short Occurrences, short Successions, List<StructureSongTonality> Examples);