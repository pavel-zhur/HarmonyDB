using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.Api.Model.VExternal1.Main;

namespace HarmonyDB.Playground.Web.Models;

public record SongsByHeaderModel : SongsByHeaderRequest
{
    public bool IncludeTrace { get; init; }

    public bool JustForm { get; set; }
}