using HarmonyDB.Index.Api.Model.VExternal1;

namespace HarmonyDB.Playground.Web.Models;

public record StructureSongModel : StructureSongRequest, ILoopModel
{
    public bool IncludeTrace { get; init; }

    public StructureViewMode ViewMode { get; init; } = StructureViewMode.Interpreted;

    public ILoopModel With(StructureViewMode viewMode) => this with { ViewMode = viewMode, };
}