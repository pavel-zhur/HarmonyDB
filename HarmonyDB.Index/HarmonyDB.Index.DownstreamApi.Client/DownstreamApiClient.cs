using HarmonyDB.Source.Api.Client;
using HarmonyDB.Source.Api.Model.V1;
using HarmonyDB.Source.Api.Model.V1.Api;
using HarmonyDB.Source.Api.Model.VInternal;
using Microsoft.Extensions.Options;
using OneShelf.Authorization.Api.Model;
using OneShelf.Common;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.DownstreamApi.Client;

public class DownstreamApiClient
{
    private readonly SecurityContext _securityContext;
    private readonly DownstreamApiClientOptions _options;
    private readonly IReadOnlyList<SourceApiClient> _clients;

    public DownstreamApiClient(IHttpClientFactory httpClientFactory, IOptions<DownstreamApiClientOptions> options, SecurityContext securityContext)
    {
        _securityContext = securityContext;
        _options = options.Value;

        _clients = _options.DownstreamSources.Select(o => new SourceApiClient(o, httpClientFactory)).ToList();
    }

    public int GetDownstreamSourceIndexBySourceKey(string sourceKey) => _options.DownstreamSources.WithIndices().Where(x => IsAvailable(x.x)).First(s => s.x.Sources.Any(s => s.Key == sourceKey)).i;

    public IEnumerable<int> GetDownstreamSourceIndices(Func<DownstreamApiClientOptions.DownstreamSourceOptions, bool> selector) => _options.DownstreamSources.WithIndices().Where(x => selector(x.x)).Where(x => IsAvailable(x.x)).Select(x => x.i);

    public string GetSourceTitle(string sourceKey) => _options.DownstreamSources.Where(IsAvailable).SelectMany(x => x.Sources).Single(s => s.Key == sourceKey).Title;
    
    public int GetDownstreamSourceIndexByExternalId(string externalId) => _options.DownstreamSources.WithIndices().Where(x => IsAvailable(x.x)).Single(s => s.x.ExternalIdPrefixes.Any(externalId.StartsWith)).i;

    public async Task PingAll()
    {
        await Task.WhenAll(Enumerable.Range(0, _options.DownstreamSources.Count).Select(V1Ping));
    }

    public async Task<GetProgressionsIndexResponse> VInternalGetProgressionsIndex(int sourceIndex, GetProgressionsIndexRequest request, CancellationToken cancellationToken)
        => await _clients[sourceIndex].VInternalGetProgressionsIndex(request, cancellationToken);

    public async Task<GetIndexHeadersResponse> VInternalGetIndexHeaders(int sourceIndex, GetIndexHeadersRequest request, CancellationToken cancellationToken)
        => await _clients[sourceIndex].VInternalGetIndexHeaders(request, cancellationToken);

    public async Task<GetSongResponse> V1GetSong(int sourceIndex, string externalId)
        => await _clients[sourceIndex].V1GetSong(_securityContext.OutputIdentity, externalId);

    public async Task<GetSongsResponse> V1GetSongs(Identity identity, int sourceIndex, IReadOnlyList<string> externalIds)
        => await _clients[sourceIndex].V1GetSongs(identity, externalIds);

    public async Task V1Ping(int sourceIndex)
        => await _clients[sourceIndex].V1Ping();

    public async Task<SearchResponse> V1Search(int sourceIndex, SearchRequest request)
        => await _clients[sourceIndex].V1Search(request);

    public async Task<SearchHeader> V1GetSearchHeader(Identity identity, int sourceIndex, string externalId)
        => await _clients[sourceIndex].V1GetSearchHeader(identity, externalId);

    public async Task<Dictionary<string, UriAttributes>> V1GetSourcesAndExternalIds(int sourceIndex, IEnumerable<Uri> uris)
        => await _clients[sourceIndex].V1GetSourcesAndExternalIds(_securityContext.OutputIdentity, uris);

    private bool IsAvailable(DownstreamApiClientOptions.DownstreamSourceOptions downstreamOptions)
    {
        var tags = _securityContext.TenantTags;

        if (downstreamOptions.TenantTagsForbidden.Intersect(tags).Any()) return false;

        if (downstreamOptions.TenantTagsRequired.Any() && !downstreamOptions.TenantTagsRequired.Intersect(tags).Any()) return false;

        return true;
    }
}