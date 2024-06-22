using HarmonyDB.Source.Api.Client;
using HarmonyDB.Source.Api.Model.V1;
using HarmonyDB.Source.Api.Model.V1.Api;
using HarmonyDB.Source.Api.Model.VInternal;
using Microsoft.Extensions.Options;
using OneShelf.Authorization.Api.Model;
using OneShelf.Common;

namespace HarmonyDB.Index.DownstreamApi.Client;

public class DownstreamApiClient
{
    private readonly DownstreamApiClientOptions _options;
    private readonly IReadOnlyList<SourceApiClient> _clients;

    public DownstreamApiClient(IHttpClientFactory httpClientFactory, IOptions<DownstreamApiClientOptions> options)
    {
        _options = options.Value;

        DownstreamSourceIndicesBySourceKey = _options.DownstreamSources
            .WithIndices()
            .SelectMany(x => x.x.Sources.Select(s => (x.i, s.Key)))
            .ToDictionary(x => x.Key, x => x.i);

        _clients = _options.DownstreamSources.Select(o => new SourceApiClient(o, httpClientFactory)).ToList();
    }

    public IReadOnlyDictionary<string, int> DownstreamSourceIndicesBySourceKey { get; }

    public IEnumerable<int> GetDownstreamSourceIndices(Func<DownstreamApiClientOptions.DownstreamSourceOptions, bool> selector) => _options.DownstreamSources.WithIndices().Where(x => selector(x.x)).Select(x => x.i);
    
    public int DownstreamSourcesCount => _options.DownstreamSources.Count;

    public string GetSourceTitle(string sourceKey) => _options.DownstreamSources.SelectMany(x => x.Sources).Single(s => s.Key == sourceKey).Title;
    
    public int GetDownstreamSourceIndex(string externalId) => _options.DownstreamSources.WithIndices().Single(s => s.x.ExternalIdPrefixes.Any(externalId.StartsWith)).i;

    public async Task<GetProgressionsIndexResponse> VInternalGetProgressionsIndex(int sourceIndex, GetProgressionsIndexRequest request, CancellationToken cancellationToken)
        => await _clients[sourceIndex].VInternalGetProgressionsIndex(request, cancellationToken);

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