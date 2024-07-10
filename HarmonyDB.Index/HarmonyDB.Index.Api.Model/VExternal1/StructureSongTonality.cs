using HarmonyDB.Index.Analysis.Models.Index;
using HarmonyDB.Source.Api.Model.V1;

namespace HarmonyDB.Index.Api.Model.VExternal1;

public record StructureSongTonality(

    // base
    string ExternalId,
    int TotalLoops,

    // new
    float[] Probabilities,
    float ScaleScore,
    float TonicScore,
    IndexHeader IndexHeader)

    : StructureSong(ExternalId, TotalLoops);