using HarmonyDB.Index.Api.Model.VExternal1.Caches;
using HarmonyDB.Source.Api.Model.V1;

namespace HarmonyDB.Index.Api.Model.VExternal1;

public record StructureSongTonality(

    // base
    string ExternalId,
    int TotalLoops,

    // new
    float[] Probabilities,
    float TonicScore,
    float ScaleScore,
    IndexHeader IndexHeader,
    int? KnownTonalityIndex)

    : StructureSong(ExternalId, TotalLoops);