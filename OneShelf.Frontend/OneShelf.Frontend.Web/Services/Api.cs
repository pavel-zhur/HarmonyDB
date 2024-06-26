using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Source.Api.Model.V1;
using ApplyRequest = OneShelf.Frontend.Api.Model.V3.Api.ApplyRequest;
using GetPdfsResponse = OneShelf.Frontend.Api.Model.V3.Api.GetPdfsResponse;
using OneShelf.Frontend.Api.Client;
using OneShelf.Frontend.Api.Model.V3.Databasish;
using OneShelf.Frontend.Api.Model.V3.Api;

namespace OneShelf.Frontend.Web.Services;

public class Api : IDisposable
{
    private readonly IdentityProvider _identityProvider;
    private readonly FrontendApiClient _frontendApiClient;
    private readonly IServiceScope _scope;

    public Api(IdentityProvider identityProvider, IServiceScopeFactory serviceScopeFactory)
    {
        _identityProvider = identityProvider;
        _scope = serviceScopeFactory.CreateScope();
        _frontendApiClient = _scope.ServiceProvider.GetRequiredService<FrontendApiClient>();
    }

    public void Dispose()
    {
        _scope.Dispose();
    }

    public async Task<Dictionary<string, Chords>> GetChords(IReadOnlyList<string> externalIds, CancellationToken cancellationToken = default)
        => (await _frontendApiClient.GetChords(_identityProvider.RequiredIdentity, externalIds, _identityProvider.NavigateToUserUnauthorizedIdentity, cancellationToken)).Songs;

    public async Task<NodeHtml> FormatPreview(string content, CancellationToken cancellationToken = default)
        => (await _frontendApiClient.FormatPreview(_identityProvider.RequiredIdentity, content, _identityProvider.NavigateToUserUnauthorizedIdentity, cancellationToken)).Output;
    
    public async Task<GetPdfsResponse> GetPdfs(bool includeData, List<GetPdfsRequestVersion> versions, bool includeInspiration = false, string? caption = null, bool reindex = false)
        => await _frontendApiClient.GetPdfs(_identityProvider.RequiredIdentity, includeData, versions, _identityProvider.NavigateToUserUnauthorizedIdentity, includeInspiration, caption, reindex);

    public async Task<List<SearchHeader>> Search(string query) => (await _frontendApiClient.Search(_identityProvider.RequiredIdentity, query, _identityProvider.NavigateToUserUnauthorizedIdentity)).Headers;

    public async Task<GetVisitedChordsResponse> GetVisitedChords(int pageIndex = 0)
        => await _frontendApiClient.GetVisitedChords(_identityProvider.RequiredIdentity, _identityProvider.NavigateToUserUnauthorizedIdentity, pageIndex);

    public async Task<GetVisitedSearchesResponse> GetVisitedSearches(int pageIndex = 0)
        => await _frontendApiClient.GetVisitedSearches(_identityProvider.RequiredIdentity, _identityProvider.NavigateToUserUnauthorizedIdentity, pageIndex);

    public async Task<Collection> Apply(ApplyRequest applyRequest, Collection? existingCollection)
    {
        applyRequest.Etag = existingCollection?.Etag;
        return (await _frontendApiClient.Apply(applyRequest, _identityProvider.NavigateToUserUnauthorizedIdentity))?.Collection ?? existingCollection!;
    }

    public async Task<bool> DeleteLikeCategory(int id) => (await _frontendApiClient.DeleteLikeCategory(_identityProvider.RequiredIdentity, id, _identityProvider.NavigateToUserUnauthorizedIdentity)).Result;

    public async Task<bool> CreateLikeCategory(LikeCategory likeCategory) => (await _frontendApiClient.CreateLikeCategory(_identityProvider.RequiredIdentity, likeCategory, _identityProvider.NavigateToUserUnauthorizedIdentity)).Result;

    public async Task<bool> UpdateLikeCategory(LikeCategory likeCategory) => (await _frontendApiClient.UpdateLikeCategory(_identityProvider.RequiredIdentity, likeCategory, _identityProvider.NavigateToUserUnauthorizedIdentity)).Result;

    public async Task<IllustrationsResponse> GetIllustrations(int? eTag, bool allVersions) =>
        await _frontendApiClient.GetIllustrations(_identityProvider.RequiredIdentity, eTag, allVersions, _identityProvider.NavigateToUserUnauthorizedIdentity);

    public async Task<IllustrationsResponse?> GetIllustrationsAnonymous() =>
        await _frontendApiClient.GetIllustrationsAnonymous();
}