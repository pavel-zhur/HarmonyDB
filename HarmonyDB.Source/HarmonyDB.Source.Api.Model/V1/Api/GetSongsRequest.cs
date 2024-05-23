using OneShelf.Authorization.Api.Model;

namespace HarmonyDB.Source.Api.Model.V1.Api;

public class GetSongsRequest : IRequestWithIdentity
{
    public required Identity Identity { get; init; }

    public required IReadOnlyList<string> ExternalIds { get; init; }
}