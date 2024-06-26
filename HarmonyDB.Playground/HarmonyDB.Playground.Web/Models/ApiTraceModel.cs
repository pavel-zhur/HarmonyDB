using HarmonyDB.Index.Api.Model.VExternal1;

namespace HarmonyDB.Playground.Web.Models;

public record SearchModel : SearchRequest
{
    public bool IncludeTrace { get; set; }

    public bool JustForm { get; set; }
}