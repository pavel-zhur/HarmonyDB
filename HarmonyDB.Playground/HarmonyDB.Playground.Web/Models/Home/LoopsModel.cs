using HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

namespace HarmonyDB.Playground.Web.Models.Home;

public record LoopsModel : LoopsRequest
{
    public bool IncludeTrace { get; init; }

    public bool JustForm { get; set; }

    public static IReadOnlyList<LoopsRequestOrdering> SupportedOrdering { get; } = new List<LoopsRequestOrdering>
    {
        LoopsRequestOrdering.LengthAscSongsDesc,
        LoopsRequestOrdering.LengthDescSongsDesc,
        LoopsRequestOrdering.LengthAscSuccessionsDesc,
        LoopsRequestOrdering.LengthDescSuccessionsDesc,
        LoopsRequestOrdering.LengthAscOccurrencesDesc,
        LoopsRequestOrdering.LengthDescOccurrencesDesc,
        LoopsRequestOrdering.LengthAscTonalityConfidenceDesc,
        LoopsRequestOrdering.LengthDescTonalityConfidenceDesc,
        LoopsRequestOrdering.SongsDesc,
        LoopsRequestOrdering.SuccessionsDesc,
        LoopsRequestOrdering.OccurrencesDesc,
        LoopsRequestOrdering.TonalityConfidenceDesc,
    };
}