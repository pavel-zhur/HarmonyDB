using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

namespace HarmonyDB.Playground.Web.Models;

public record StructureSongModel : SongRequest, ILoopModel
{
    public bool IncludeTrace { get; init; }

    public StructureViewMode ViewMode { get; init; } = StructureViewMode.Interpreted;

    public ILoopModel With(StructureViewMode viewMode) => this with { ViewMode = viewMode, };
}