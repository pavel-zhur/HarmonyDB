using HarmonyDB.Index.Api.Model.VExternal1;

namespace HarmonyDB.Playground.Web.Models;

public record StructureSongModel : StructureSongRequest
{
    public bool IncludeTrace { get; init; }
}