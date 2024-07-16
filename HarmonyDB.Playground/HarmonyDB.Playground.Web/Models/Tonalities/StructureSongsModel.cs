using HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

namespace HarmonyDB.Playground.Web.Models.Tonalities;

public record StructureSongsModel : SongsRequest
{
    public bool IncludeTrace { get; init; }

    public bool JustForm { get; set; }
}