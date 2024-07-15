using HarmonyDB.Index.Api.Model.VExternal1;

namespace HarmonyDB.Playground.Web.Models;

public record StructureLoopsModel : StructureLoopsRequest, ILoopModel
{
    public bool IncludeTrace { get; init; }

    public bool JustForm { get; set; }

    public StructureViewMode ViewMode { get; init; } = StructureViewMode.Interpreted;

    public ILoopModel With(StructureViewMode viewMode) => this with { ViewMode = viewMode, };
}