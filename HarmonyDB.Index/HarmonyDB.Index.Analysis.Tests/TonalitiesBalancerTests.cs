using HarmonyDB.Index.Analysis.Services;
using Microsoft.Extensions.Logging;

namespace HarmonyDB.Index.Analysis.Tests;

public class TonalitiesBalancerTests(ILogger<TonalitiesBalancerTests> logger, TonalitiesBalancer tonalitiesBalancer)
{
    [Fact]
    public async Task Try()
    {
        await tonalitiesBalancer.Balance([
                ("L1", "S1", 5, 10, 5),
                ("L2", "S1", 1, 10, 5),
                ("L3", "S1", 8, 10, 5),
                
                ("L1", "S2", 6, 10, 5),
                ("L2", "S2", 2, 10, 5),
                ("L3", "S2", 9, 10, 5)
            ],
            new Dictionary<string, (float[], bool)>
            {
                { "S1", (Enumerable.Range(0, 24).Select(x => x == 2 ? 1f / 5 : x == 5 ? 4f / 5 : 0).ToArray(), true) },
                { "S2", (tonalitiesBalancer.CreateNewProbabilities(false), false) },
            },
            new Dictionary<string, (float[], bool)>
            {
                { "L1", (tonalitiesBalancer.CreateNewProbabilities(false), false) },
                { "L2", (tonalitiesBalancer.CreateNewProbabilities(false), false) },
                { "L3", (tonalitiesBalancer.CreateNewProbabilities(false), false) },
            });
    }
}