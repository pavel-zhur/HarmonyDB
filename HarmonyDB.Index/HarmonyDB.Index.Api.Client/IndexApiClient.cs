using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VInternal;
using HarmonyDB.Source.Api.Model;
using HarmonyDB.Source.Api.Model.V1;
using HarmonyDB.Source.Api.Model.V1.Api;
using Microsoft.Extensions.Options;
using OneShelf.Authorization.Api.Model;
using OneShelf.Common.Api.Client;

namespace HarmonyDB.Index.Api.Client;

public class IndexApiClient : ApiClientBase<IndexApiClient>
{
    public IndexApiClient(IHttpClientFactory httpClientFactory, IOptions<ApiClientOptions<IndexApiClient>> options)
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

    public async Task<GetSongsResponse> V1GetSongs(Identity identity, List<string> externalIds)
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

    public async Task<string?> GetLyrics(string url)
        => (await PostWithCode<GetLyricsRequest, GetLyricsResponse>(IndexApiUrls.VInternalGetLyrics, new()
        {
            Url = url,
        })).Lyrics;

    public async Task<TryImportResponse> TryImport(string url)
        => await PostWithCode<TryImportRequest, TryImportResponse>(IndexApiUrls.VInternalTryImport, new()
        {
            Url = url,
        });
}