using HarmonyDB.Source.Api.Client;
using HarmonyDB.Source.Api.Model.V1;
using HarmonyDB.Source.Api.Model.V1.Api;
using Microsoft.Extensions.Options;
using OneShelf.Authorization.Api.Model;
using OneShelf.Common;

namespace HarmonyDB.Index.SourcesApiClient;

public class SourcesApiClient
{
    private readonly SourcesApiClientOptions _options;
    private readonly IReadOnlyList<SourceApiClient> _clients;

    public SourcesApiClient(IHttpClientFactory httpClientFactory, IOptions<SourcesApiClientOptions> options)
    {
        _options = options.Value;

        SourceIndices = _options.Sources
            .WithIndices()
            .SelectMany(x => x.x.Sources.Select(s => (x.i, s)))
            .ToDictionary(x => x.s, x => x.i);

        _clients = _options.Sources.Select(o => new SourceApiClient(o, httpClientFactory)).ToList();
    }

    public IReadOnlyDictionary<string, int> SourceIndices { get; }

    public IEnumerable<int> SearchableIndices => _options.Sources.WithIndices().Where(x => x.x.SupportsSearch).Select(x => x.i);

    public int SourcesCount => _options.Sources.Count;

    public async Task<GetSongResponse> V1GetSong(Identity identity, int sourceIndex, string externalId)
        => await _clients[sourceIndex].V1GetSong(identity, externalId);

    public async Task<GetSongsResponse> V1GetSongs(Identity identity, int sourceIndex, IReadOnlyList<string> externalIds)
        => await _clients[sourceIndex].V1GetSongs(identity, externalIds);

    public async Task V1Ping(int sourceIndex)
        => await _clients[sourceIndex].V1Ping();

    public async Task<SearchResponse> V1Search(int sourceIndex, SearchRequest request)
        => await _clients[sourceIndex].V1Search(request);

    public async Task<SearchHeader> V1GetSearchHeader(Identity identity, int sourceIndex, string externalId)
        => await _clients[sourceIndex].V1GetSearchHeader(identity, externalId);

    public async Task<Dictionary<string, UriAttributes>> V1GetSourcesAndExternalIds(Identity identity, int sourceIndex, IEnumerable<Uri> uris)
        => await _clients[sourceIndex].V1GetSourcesAndExternalIds(identity, uris);
}