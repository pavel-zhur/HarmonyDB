using System.Text.Json;
using HarmonyDB.Index.DownstreamApi.Client;
using HarmonyDB.Source.Api.Model.VInternal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Common.Compression;

namespace HarmonyDB.Index.Api.Functions.VDev;

public class IndexFunctions
{
    private readonly ILogger<IndexFunctions> _logger;
    private readonly DownstreamApiClient _downstreamApiClient;

    public IndexFunctions(ILogger<IndexFunctions> logger, DownstreamApiClient downstreamApiClient)
    {
        _logger = logger;
        _downstreamApiClient = downstreamApiClient;
    }

    [Function("VDevSaveProgressionsIndex")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        var results = (await Task.WhenAll(_downstreamApiClient.GetDownstreamSourceIndices(x => x.AreProgressionsProvidedForIndexing)
                .Select(async i =>
                {
                    Dictionary<string, byte[]> progressions = new();
                    var iteration = 0;
                    while (true)
                    {
                        var result = await _downstreamApiClient.VInternalGetProgressionsIndex(i);
                        foreach (var progression in result.Progressions)
                        {
                            progressions[progression.Key] = progression.Value;
                        }

                        _logger.LogInformation("Iteration {iteration}", iteration++);

                        if (result.NextToken == null) break;
                    }

                    return progressions;
                })))
            .SelectMany(x => x)
            .ToDictionary(x => x.Key, x => x.Value);

        await File.WriteAllBytesAsync("progressionsIndex.bin", await CompressionTools.Compress(JsonSerializer.Serialize(results)));
        return new OkResult();
    }
}