using HarmonyDB.Index.Api.Model.VExternal1;

namespace HarmonyDB.Playground.Web.Models;

public record StructureSongsModel : StructureSongsRequest
{
    public bool IncludeTrace { get; init; }

    public bool JustForm { get; set; }
}