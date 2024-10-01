using HarmonyDB.Index.DownstreamApi.Client;
using HarmonyDB.Source.Api.Model.V1;
using HarmonyDB.Source.Api.Model.V1.Api;
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

    public IndexHeader? PrepareForOutput(IndexHeader header)
    {
        header = header with { Source = _downstreamApiClient.GetSourceTitle(header.Source) };
        if (string.IsNullOrWhiteSpace(header.Title)
            || header.Artists?.Any(a => !string.IsNullOrWhiteSpace(a)) != true
            || string.IsNullOrWhiteSpace(header.Source)) 
            return null;

        return header;
    }

    public async Task<GetSourcesAndExternalIdsResponse> GetSourcesAndExternalIds(IReadOnlyList<Uri> uris)
    {
        var results = await Task.WhenAll(_downstreamApiClient.GetDownstreamSourceIndices(_ => true)
            .Select(x => _downstreamApiClient.V1GetSourcesAndExternalIds(x, uris)));

        var all = results
            .WithIndices()
            .SelectMany(s => s.x
                .Where(x => _downstreamApiClient.GetDownstreamSourceIndexBySourceKey(x.Value.Source) == s.i)
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

    public async Task<GetSongResponse> GetSong(string externalId)
    {
        var sourceIndex = _downstreamApiClient.GetDownstreamSourceIndexByExternalId(externalId);
        var getSongResponse = await _downstreamApiClient.V1GetSong(sourceIndex, externalId);
        getSongResponse.Song.Source = _downstreamApiClient.GetSourceTitle(getSongResponse.Song.Source);
        getSongResponse.Song.Output.Fix0_Compress();
        return getSongResponse;
    }
}