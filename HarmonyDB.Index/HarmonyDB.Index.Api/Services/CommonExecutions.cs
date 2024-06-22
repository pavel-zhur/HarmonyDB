using HarmonyDB.Index.DownstreamApi.Client;
using HarmonyDB.Source.Api.Client;
using HarmonyDB.Source.Api.Model.V1.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Common;

namespace HarmonyDB.Index.Api.Services;

public class CommonExecutions
{
    private readonly ILogger<CommonExecutions> _logger;
    private readonly DownstreamApiClient _downstreamApiClient;

    public CommonExecutions(ILogger<CommonExecutions> logger, DownstreamApiClient downstreamApiClient)
    {
        _logger = logger;
        _downstreamApiClient = downstreamApiClient;
    }

    public async Task<GetSourcesAndExternalIdsResponse> GetSourcesAndExternalIds(GetSourcesAndExternalIdsRequest request)
    {
        var results = await Task.WhenAll(Enumerable.Range(0, _downstreamApiClient.DownstreamSourcesCount)
            .Select(x => _downstreamApiClient.V1GetSourcesAndExternalIds(request.Identity, x, request.Uris)));

        var all = results
            .WithIndices()
            .SelectMany(s => s.x
                .Where(x => _downstreamApiClient.DownstreamSourceIndicesBySourceKey[x.Value.Source] == s.i)
                .Select(x =>
                {
                    x.Value.Source = _downstreamApiClient.GetSourceTitle(x.Value.Source);
                    return x;
                }))
            .ToDictionary(x => x.Key, x => x.Value);

        return new()
        {
            Attributes = all,
        };
    }

    public async Task<GetSongResponse> GetSong(GetSongRequest request)
    {
        var sourceIndex = _downstreamApiClient.GetDownstreamSourceIndex(request.ExternalId);
        var getSongResponse = await _downstreamApiClient.V1GetSong(request.Identity, sourceIndex, request.ExternalId);
        getSongResponse.Song.Source = _downstreamApiClient.GetSourceTitle(getSongResponse.Song.Source);
        return getSongResponse;
    }
}