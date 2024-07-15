namespace HarmonyDB.Playground.Web.Models.Tonalities;

public interface ILoopModel
{
    StructureViewMode ViewMode { get; init; }

    ILoopModel With(StructureViewMode viewMode);
}