using HarmonyDB.Index.Analysis.Models.Index;

namespace HarmonyDB.Index.Api.Model.VExternal1;

public record StructureSongTonality(
    string ExternalId,
    int TotalLoops,
    float[] Probabilities,
    float ScaleScore,
    float TonicScore,
    StructureSongKnownTonality? KnownTonality,
    float? Rating)
    : StructureSong(ExternalId, TotalLoops);