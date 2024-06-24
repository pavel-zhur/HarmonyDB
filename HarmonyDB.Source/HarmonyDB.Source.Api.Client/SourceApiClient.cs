using HarmonyDB.Source.Api.Model;
using HarmonyDB.Source.Api.Model.V1;
using HarmonyDB.Source.Api.Model.V1.Api;
using HarmonyDB.Source.Api.Model.VInternal;
using OneShelf.Authorization.Api.Model;
using OneShelf.Common.Api.Client;

namespace HarmonyDB.Source.Api.Client;

public class SourceApiClient : ApiClientBase<SourceApiClient>
{
    public SourceApiClient(ApiClientOptions<SourceApiClient> options, IHttpClientFactory httpClientFactory) 
        : base(options, httpClientFactory)
    {
    }

    public async Task<string> V1GetSongsDirect(string request)
        => await PostDirect(SourceApiUrls.V1GetSongs, request);

    public async Task<GetSongResponse> V1GetSong(Identity identity, string externalId)
        => await Post<GetSongRequest, GetSongResponse>(SourceApiUrls.V1GetSong, new()
        {
            Identity = identity,
            ExternalId = externalId,
        });

    public async Task<GetProgressionsIndexResponse> VInternalGetProgressionsIndex(GetProgressionsIndexRequest request, CancellationToken cancellationToken)
        => await PostWithCode<GetProgressionsIndexRequest, GetProgressionsIndexResponse>(SourceApiUrls.VInternalGetProgressionsIndex, request, cancellationToken);

    public async Task<GetIndexHeadersResponse> VInternalGetIndexHeaders(GetIndexHeadersRequest request, CancellationToken cancellationToken)
        => await PostWithCode<GetIndexHeadersRequest, GetIndexHeadersResponse>(SourceApiUrls.VInternalGetIndexHeaders, request, cancellationToken);

    public async Task<GetSongsResponse> V1GetSongs(Identity identity, IReadOnlyList<string> externalIds)
        => await Post<GetSongsRequest, GetSongsResponse>(SourceApiUrls.V1GetSongs, new()
        {
            Identity = identity,
            ExternalIds = externalIds,
        });

    public async Task V1Ping()
        => await Ping(SourceApiUrls.V1Ping);

    public async Task<SearchResponse> V1Search(SearchRequest request)
        => await Post<SearchRequest, SearchResponse>(SourceApiUrls.V1Search, request);

    public async Task<SearchHeader> V1GetSearchHeader(Identity identity, string externalId)
        => (await Post<GetSearchHeaderRequest, GetSearchHeaderResponse>(SourceApiUrls.V1GetSearchHeader, new()
        {
            Identity = identity,
            ExternalId = externalId,
        })).SearchHeader;

    public async Task<Dictionary<string, UriAttributes>> V1GetSourcesAndExternalIds(Identity identity, IEnumerable<Uri> uris)
        => (await Post<GetSourcesAndExternalIdsRequest, GetSourcesAndExternalIdsResponse>(SourceApiUrls.V1GetSourcesAndExternalIds, new()
        {
            Identity = identity,
            Uris = uris.ToList(),
        })).Attributes;
}