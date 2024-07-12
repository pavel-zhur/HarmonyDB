using HarmonyDB.Index.Api.Model.VExternal1;

namespace HarmonyDB.Playground.Web.Models;

public record StructureLoopsModel : StructureLoopsRequest, ILoopModel
{
    public bool IncludeTrace { get; init; }

    public bool JustForm { get; set; }

    public StructureLoopViewMode ViewMode { get; init; } = StructureLoopViewMode.Interpreted;

    public ILoopModel With(StructureLoopViewMode viewMode) => this with { ViewMode = viewMode, };
}