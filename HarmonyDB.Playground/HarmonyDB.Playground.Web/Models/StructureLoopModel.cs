using HarmonyDB.Index.Api.Model.VExternal1;

namespace HarmonyDB.Playground.Web.Models;

public record StructureLoopModel : StructureLoopRequest, ILoopModel
{
    public bool IncludeTrace { get; init; }

    public StructureLoopViewMode ViewMode { get; init; } = StructureLoopViewMode.Tonality;

    public ILoopModel With(StructureLoopViewMode viewMode) => this with { ViewMode = viewMode, };
}