using System.Net.Http.Json;
using System.Text.Json;
using HarmonyDB.Source.Api.Model.V1.Api;
using Microsoft.Extensions.Options;
using OneShelf.Authorization.Api.Model;
using OneShelf.Common.Api.Client;
using OneShelf.Frontend.Api.Model.V3.Api;
using OneShelf.Frontend.Api.Model.V3.Databasish;
using OneShelf.Sources.Self.Api.Model.V1;
using FormatPreviewRequest = OneShelf.Frontend.Api.Model.V3.Api.FormatPreviewRequest;
using FormatPreviewResponse = OneShelf.Frontend.Api.Model.V3.Api.FormatPreviewResponse;
using GetPdfsRequest = OneShelf.Frontend.Api.Model.V3.Api.GetPdfsRequest;
using GetPdfsResponse = OneShelf.Frontend.Api.Model.V3.Api.GetPdfsResponse;

namespace OneShelf.Frontend.Api.Client;

public class FrontendApiClient : ApiClientBase<FrontendApiClient>
{
    public FrontendApiClient(IHttpClientFactory httpClientFactory, IOptions<ApiClientOptions<FrontendApiClient>> options)
        : base(options, httpClientFactory)
    {
    }

    public async Task<GetSongsResponse> GetChords(Identity identity, IReadOnlyList<string> externalIds, Action? unauthorized, CancellationToken cancellationToken = default)
        => await Post<GetSongsRequest, GetSongsResponse>(ApiUrls.V3GetChords, new() { ExternalIds = externalIds, Identity = identity }, unauthorized, cancellationToken);

    public async Task<FormatPreviewResponse> FormatPreview(Identity identity, string content, Action? unauthorized, CancellationToken cancellationToken = default)
        => await Post<FormatPreviewRequest, FormatPreviewResponse>(ApiUrls.V3FormatPreview, new() { Content = content, Identity = identity }, unauthorized, cancellationToken);

    public async Task<GetPdfsResponse> GetPdfs(Identity identity, bool includeData, List<GetPdfsRequestVersion> versions, Action? unauthorized, bool includeInspiration = false, string? caption = null, bool reindex = false)
        => await Post<GetPdfsRequest, GetPdfsResponse>(ApiUrls.V3GetPdfs, new() { IncludeData = includeData, Identity = identity, Versions = versions, Caption = caption, IncludeInspiration = includeInspiration, Reindex = reindex, }, unauthorized);

    public async Task<SearchResponse> Search(Identity identity, string query, Action? unauthorized)
        => await Post<SearchRequest, SearchResponse>(ApiUrls.V3Search, new() { Query = query, Identity = identity }, unauthorized);

    public async Task<GetVisitedChordsResponse> GetVisitedChords(Identity identity, Action? unauthorized, int pageIndex = 0)
        => await Post<GetVisitedChordsRequest, GetVisitedChordsResponse>(ApiUrls.V3GetVisitedChords, new() { PageIndex = pageIndex, Identity = identity }, unauthorized);

    public async Task<GetVisitedSearchesResponse> GetVisitedSearches(Identity identity, Action? unauthorized, int pageIndex = 0)
        => await Post<GetVisitedSearchesRequest, GetVisitedSearchesResponse>(ApiUrls.V3GetVisitedSearches, new() { PageIndex = pageIndex, Identity = identity }, unauthorized);

    public async Task<ApplyResponse> Apply(ApplyRequest applyRequest, Action? unauthorized)
    {
        return await Post<ApplyRequest, ApplyResponse>(ApiUrls.V3Apply, applyRequest, unauthorized);
    }

    public async Task<DeleteLikeCategoryResponse> DeleteLikeCategory(Identity identity, int id, Action? unauthorized) =>
        await Post<DeleteLikeCategoryRequest, DeleteLikeCategoryResponse>(ApiUrls.V3DeleteLikeCategory, new()
        {
            Identity = identity,
            LikeCategoryId = id,
        }, unauthorized);

    public async Task<CreateLikeCategoryResponse> CreateLikeCategory(Identity identity, LikeCategory likeCategory, Action? unauthorized) =>
        await Post<CreateLikeCategoryRequest, CreateLikeCategoryResponse>(ApiUrls.V3CreateLikeCategory, new()
        {
            Identity = identity,
            LikeCategory = likeCategory,
        }, unauthorized);

    public async Task<UpdateLikeCategoryResponse> UpdateLikeCategory(Identity identity, LikeCategory likeCategory, Action? unauthorized) =>
        await Post<UpdateLikeCategoryRequest, UpdateLikeCategoryResponse>(ApiUrls.V3UpdateLikeCategory, new()
        {
            Identity = identity,
            LikeCategory = likeCategory,
        }, unauthorized);

    public async Task<IllustrationsResponse> GetIllustrations(Identity identity, int? eTag, bool allVersions, Action? unauthorized) =>
        await Post<IllustrationsRequest, IllustrationsResponse>(ApiUrls.V3Illustrations, new()
        {
            Identity = identity,
            Etag = eTag,
            AllVersions = allVersions,
        }, unauthorized);

    public async Task<IllustrationsResponse?> GetIllustrationsAnonymous() =>
        await Get<IllustrationsResponse>(ApiUrls.V3IllustrationsAnonymous);
}