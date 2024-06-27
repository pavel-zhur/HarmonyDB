using HarmonyDB.Source.Api.Model.V1;

namespace HarmonyDB.Index.Api.Model.VExternal1;

public record SongsByHeaderResponse : PagedResponseBase
{
    public required List<IndexHeader> Songs { get; init; }
}