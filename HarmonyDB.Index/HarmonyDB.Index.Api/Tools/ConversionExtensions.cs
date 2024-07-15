using HarmonyDB.Index.Analysis.Models.Structure;
using HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

namespace HarmonyDB.Index.Api.Tools;

public static class ConversionExtensions
{
    public static Link ToModel(this StructureLink link)
        => new(link.Normalized, link.ExternalId, link.NormalizationRoot, link.Occurrences, link.Successions, link.Coverage);
}