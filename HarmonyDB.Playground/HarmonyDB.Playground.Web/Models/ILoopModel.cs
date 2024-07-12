namespace HarmonyDB.Playground.Web.Models;

public interface ILoopModel
{
    StructureLoopViewMode ViewMode { get; init; }

    ILoopModel With(StructureLoopViewMode viewMode);
}