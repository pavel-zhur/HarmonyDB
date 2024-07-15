using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

namespace HarmonyDB.Playground.Web.Models;

public record StructureSongsModel : SongsRequest
{
    public bool IncludeTrace { get; init; }

    public bool JustForm { get; set; }
}