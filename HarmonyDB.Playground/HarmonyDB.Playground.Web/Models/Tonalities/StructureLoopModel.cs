using HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

namespace HarmonyDB.Playground.Web.Models.Tonalities;

public record StructureLoopModel : LoopRequest, ILoopModel
{
    public bool IncludeTrace { get; init; }

    public StructureViewMode ViewMode { get; init; } = StructureViewMode.Interpreted;

    public ILoopModel With(StructureViewMode viewMode) => this with { ViewMode = viewMode, };
}