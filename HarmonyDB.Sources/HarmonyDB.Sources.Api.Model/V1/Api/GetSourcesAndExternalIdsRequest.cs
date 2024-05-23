using OneShelf.Authorization.Api.Model;

namespace HarmonyDB.Source.Api.Model.V1.Api;

public class GetSourcesAndExternalIdsRequest : IRequestWithIdentity
{
    public required Identity Identity { get; init; }

    public required IReadOnlyList<Uri> Uris { get; init; }
}