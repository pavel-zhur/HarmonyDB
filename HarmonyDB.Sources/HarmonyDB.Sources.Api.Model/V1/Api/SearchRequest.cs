using OneShelf.Authorization.Api.Model;

namespace HarmonyDB.Sources.Api.Model.V1.Api;

public class SearchRequest : IRequestWithIdentity
{
    public required Identity Identity { get; init; }

    public required string Query { get; init; }
}