using HarmonyDB.Index.BusinessLogic.Services;
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
    private readonly SourceResolver _sourceResolver;

    public CommonExecutions(ILogger<CommonExecutions> logger, DownstreamApiClient downstreamApiClient, SourceResolver sourceResolver)
    {
        _logger = logger;
        _downstreamApiClient = downstreamApiClient;
        _sourceResolver = sourceResolver;
    }

    public async Task<GetSourcesAndExternalIdsResponse> GetSourcesAndExternalIds(GetSourcesAndExternalIdsRequest request)
    {
        var results = await Task.WhenAll(Enumerable.Range(0, _downstreamApiClient.SourcesCount)
            .Select(x => _downstreamApiClient.V1GetSourcesAndExternalIds(request.Identity, x, request.Uris)));
        var all = results
            .WithIndices()
            .SelectMany(s => s.x.Where(x => _downstreamApiClient.SourceIndices[x.Value.Source] == s.i))
            .ToDictionary(x => x.Key, x => x.Value);

        return new()
        {
            Attributes = all,
        };
    }

    public async Task<GetSongResponse> GetSong(GetSongRequest request)
        => await _downstreamApiClient.V1GetSong(
            request.Identity,
            _downstreamApiClient.SourceIndices[_sourceResolver.GetSource(request.ExternalId)], request.ExternalId);
}