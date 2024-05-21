using HarmonyDB.Sources.Api.Model;
using HarmonyDB.Sources.Api.Model.V1;
using HarmonyDB.Sources.Api.Model.V1.Api;
using OneShelf.Authorization.Api.Model;
using OneShelf.Common.Api.Client;

namespace HarmonyDB.Sources.Api.Client;

internal class SourceApiClient : ApiClientBase<SourceApiClient>
{
    public SourceApiClient(ApiClientOptions<SourceApiClient> options, IHttpClientFactory httpClientFactory) 
        : base(options, httpClientFactory)
    {
    }

    public async Task<GetSongResponse> V1GetSong(Identity identity, string externalId)
        => await Post<GetSongRequest, GetSongResponse>(SourcesApiUrls.V1GetSong, new()
        {
            Identity = identity,
            ExternalId = externalId,
        });
    public async Task<GetSongsResponse> V1GetSongs(Identity identity, IReadOnlyList<string> externalIds)
        => await Post<GetSongsRequest, GetSongsResponse>(SourcesApiUrls.V1GetSongs, new()
        {
            Identity = identity,
            ExternalIds = externalIds,
        });

    public async Task V1Ping()
        => await Ping(SourcesApiUrls.V1Ping);

    public async Task<SearchResponse> V1Search(SearchRequest request)
        => await Post<SearchRequest, SearchResponse>(SourcesApiUrls.V1Search, request);

    public async Task<SearchHeader> V1GetSearchHeader(Identity identity, string externalId)
        => (await Post<GetSearchHeaderRequest, GetSearchHeaderResponse>(SourcesApiUrls.V1GetSearchHeader, new()
        {
            Identity = identity,
            ExternalId = externalId,
        })).SearchHeader;

    public async Task<Dictionary<string, UriAttributes>> V1GetSourcesAndExternalIds(Identity identity, IEnumerable<Uri> uris)
        => (await Post<GetSourcesAndExternalIdsRequest, GetSourcesAndExternalIdsResponse>(SourcesApiUrls.V1GetSourcesAndExternalIds, new()
        {
            Identity = identity,
            Uris = uris.ToList(),
        })).Attributes;
}