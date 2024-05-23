using HarmonyDB.Index.Analysis.Models.CompactV1;

namespace HarmonyDB.Source.Api.Model.VInternal;

public class GetProgressionsIndexResponse
{
    public required IReadOnlyDictionary<string, CompactChordsProgression> Progressions { get; init; }
}