using HarmonyDB.Index.Api.Model.VExternal1.Main;

namespace HarmonyDB.Playground.Web.Models.Home;

public record LoopsModel : LoopsRequest
{
    public bool IncludeTrace { get; init; }

    public bool JustForm { get; set; }

    public bool IncludeRootsStatistics { get; init; }
}