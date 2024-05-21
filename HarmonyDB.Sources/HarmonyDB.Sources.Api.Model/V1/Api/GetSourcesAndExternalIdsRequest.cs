using OneShelf.Authorization.Api.Model;

namespace HarmonyDB.Sources.Api.Model.V1.Api;

public class GetSourcesAndExternalIdsRequest : IRequestWithIdentity
{
    public required Identity Identity { get; init; }

    public required IReadOnlyList<Uri> Uris { get; init; }
}