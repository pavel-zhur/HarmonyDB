using HarmonyDB.Index.Api.Model.VExternal1;

namespace HarmonyDB.Playground.Web.Models;

public record StructureLoopModel : StructureLoopRequest
{
    public bool IncludeTrace { get; init; }
}