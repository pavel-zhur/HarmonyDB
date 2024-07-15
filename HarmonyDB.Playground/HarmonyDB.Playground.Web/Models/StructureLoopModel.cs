using HarmonyDB.Index.Api.Model.VExternal1;

namespace HarmonyDB.Playground.Web.Models;

public record StructureLoopModel : StructureLoopRequest, ILoopModel
{
    public bool IncludeTrace { get; init; }

    public StructureViewMode ViewMode { get; init; } = StructureViewMode.Interpreted;

    public ILoopModel With(StructureViewMode viewMode) => this with { ViewMode = viewMode, };
}