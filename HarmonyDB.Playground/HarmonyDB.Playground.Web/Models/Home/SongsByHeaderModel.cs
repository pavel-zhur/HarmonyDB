using HarmonyDB.Index.Api.Model.VExternal1.Main;

namespace HarmonyDB.Playground.Web.Models.Home;

public record SongsByHeaderModel : SongsByHeaderRequest
{
    public bool IncludeTrace { get; init; }

    public bool JustForm { get; set; }
}