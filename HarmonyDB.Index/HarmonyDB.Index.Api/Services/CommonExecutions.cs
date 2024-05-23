using HarmonyDB.Index.BusinessLogic.Services;
using HarmonyDB.Source.Api.Client;
using HarmonyDB.Source.Api.Model.V1.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Common;

namespace HarmonyDB.Index.Api.Services;

public class CommonExecutions
{
    private readonly ILogger<CommonExecutions> _logger;
    private readonly SourcesApiClient.SourcesApiClient _sourcesApiClient;
    private readonly SourceResolver _sourceResolver;

    public CommonExecutions(ILogger<CommonExecutions> logger, SourcesApiClient.SourcesApiClient sourcesApiClient, SourceResolver sourceResolver)
    {
        _logger = logger;
        _sourcesApiClient = sourcesApiClient;
        _sourceResolver = sourceResolver;
    }

    public async Task<GetSourcesAndExternalIdsResponse> GetSourcesAndExternalIds(GetSourcesAndExternalIdsRequest request)
    {
        var results = await Task.WhenAll(Enumerable.Range(0, _sourcesApiClient.SourcesCount)
            .Select(x => _sourcesApiClient.V1GetSourcesAndExternalIds(request.Identity, x, request.Uris)));
        var all = results
            .WithIndices()
            .SelectMany(s => s.x.Where(x => _sourcesApiClient.SourceIndices[x.Value.Source] == s.i))
            .ToDictionary(x => x.Key, x => x.Value);

        return new()
        {
            Attributes = all,
        };
    }

    public async Task<GetSongResponse> GetSong(GetSongRequest request)
        => await _sourcesApiClient.V1GetSong(
            request.Identity,
            _sourcesApiClient.SourceIndices[_sourceResolver.GetSource(request.ExternalId)], request.ExternalId);
}