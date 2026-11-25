using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Shelf.Model.AnalysisTools;
using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Shelf.Tests.Api;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Chords;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;

namespace HarmonyDB.Shelf.Tests.AnalysisTools;

/// <summary>
/// An item's analysis by the index's tools, as the person is given it.
/// </summary>
public sealed class AnalysisToolsTests
{
    [Fact]
    public async Task ALoopOccurringFromSeveralNotesIsGivenOnceForEach()
    {
        const string id = "test-loops";
        var probabilities = Enumerable.Range(0, 24).Select(i => i == 6 ? 0.9f : 0.1f / 23).ToArray();
        var index = new FakeIndex
        {
            Answer = (operation, _) => operation switch
            {
                "V2GetHeaders" => (HttpStatusCode.OK, new GetHeadersResponse { Headers = [new() { Id = id, State = ItemState.Open, Header = FakeIndex.Header(id, "A source") }] }),
                "VExternal1TonalitiesSong" => (HttpStatusCode.OK, new
                {
                    song = new { externalId = id, totalLoops = 2, probabilities, tonicScore = 0.5f, scaleScore = 0.5f, knownTonalityIndex = (int?)null },
                    loops = new[]
                    {
                        new { normalized = "loop-a", length = 4, totalOccurrences = 9f, totalSuccessions = 3f, averageCoverage = 0.2f, totalSongs = 7, probabilities, scaleScore = 0.5f, tonicScore = 0.5f },
                    },
                    links = new[]
                    {
                        new { normalized = "loop-a", externalId = id, normalizationRoot = 3, occurrences = 2f, successions = 1f, coverage = 0.1f },
                        new { normalized = "loop-a", externalId = id, normalizationRoot = 10, occurrences = 4f, successions = 2f, coverage = 0.3f },
                    },
                }),
                _ => (HttpStatusCode.InternalServerError, null),
            },
        };
        using var api = index.Api(new ShelfApiFactory());
        using var client = api.CreateClient();

        var response = await client.GetAsync($"/api/analysis/chords/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var analysis = (await response.Content.ReadFromJsonAsync<ChordsAnalysis>(Browser.Json))!;
        Assert.Equal(ChordsState.Open, analysis.State);
        Assert.True(analysis.Analysed);
        Assert.Equal([("loop-a", "G", 0.3f), ("loop-a", "C", 0.1f)], analysis.Loops.Select(x => (x.Normalized, x.NormalizationRoot, x.Coverage)));
    }
}
