using OneShelf.Authorization.Api.Model;

namespace HarmonyDB.Source.Api.Model.V1.Api;

public class SearchRequest : IRequestWithIdentity
{
    public required Identity Identity { get; init; }

    public required string Query { get; init; }
}