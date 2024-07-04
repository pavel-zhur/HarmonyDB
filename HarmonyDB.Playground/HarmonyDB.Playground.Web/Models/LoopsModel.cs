using HarmonyDB.Index.Api.Model.VExternal1;

namespace HarmonyDB.Playground.Web.Models;

public record LoopsModel : LoopsRequest
{
    public bool IncludeTrace { get; init; }

    public bool JustForm { get; set; }

    public bool IncludeRootsStatistics { get; init; }
}