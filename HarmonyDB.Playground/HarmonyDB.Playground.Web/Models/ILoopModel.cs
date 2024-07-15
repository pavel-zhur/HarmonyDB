namespace HarmonyDB.Playground.Web.Models;

public interface ILoopModel
{
    StructureViewMode ViewMode { get; init; }

    ILoopModel With(StructureViewMode viewMode);
}