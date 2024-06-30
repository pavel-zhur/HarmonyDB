using System.Runtime.InteropServices;
using System.Xml.Serialization;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using Microsoft.Extensions.Logging;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Tests;

public class LoopNormalizationTests(ILogger<LoopNormalizationTests> logger)
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void NormalizationIsCorrectForMultipleOptions(int repetitions)
    {
        var minNormalizationShift = int.MaxValue;
        var maxNormalizationShift = int.MinValue;
        const int length = 3;
        for (var i = 0; i < 100; i++)
        {
            ReadOnlyMemory<CompactHarmonyMovement> movements = Enumerable.Range(0, length)
                .Select(_ => new CompactHarmonyMovement
                {
                    ToType = (ChordType)Random.Shared.Next(0, 8),
                    FromType = (ChordType)Random.Shared.Next(0, 8),
                    RootDelta = (byte)Random.Shared.Next(0, 12),
                })
                .ToList()
                .AsEnumerable()
                .SelectSingle(x => Enumerable.Range(0, repetitions - 1)
                    .Select(_ => x)
                    .Aggregate(x, (x, y) => x.Concat(y)))
                .ToArray();

            Loop loop = new()
            {
                Progression = movements,
                Coverage = [],
                FoundFirsts = [],
                IsCompound = false,
                Occurrences = 0,
                SequenceIndex = 0,
                Start = 0,
                Successions = 0,
            };

            Loop.GetNormalizedProgression(loop.Progression, out var normalizationShift, out var invariants);
            minNormalizationShift = Math.Min(minNormalizationShift, normalizationShift);
            maxNormalizationShift = Math.Max(maxNormalizationShift, normalizationShift);
            Assert.Equal(repetitions, invariants);
        }

        // these assertions may very rarely fail, when an edge case never happens in 1000 repetitions. Almost impossible.
        Assert.Equal(0, minNormalizationShift);
        Assert.Equal(length - 1, maxNormalizationShift);
    }

    [Fact]
    public void NormalizedIsStable1000()
    {
        const int length = 10;
        (Loop loop1, Loop loop2) NewLoop()
        {
            ReadOnlyMemory<CompactHarmonyMovement> movements = Enumerable.Range(0, length).Select(_ => new CompactHarmonyMovement
            {
                ToType = (ChordType)Random.Shared.Next(0, 8),
                FromType = (ChordType)Random.Shared.Next(0, 8),
                RootDelta = (byte)Random.Shared.Next(0, 12),
            }).ToList().SelectSingle(x => x.Concat(x)).ToArray();

            Loop loop = new()
            {
                Progression = movements.Slice(0, length),
                Coverage = [],
                FoundFirsts = [],
                IsCompound = false,
                Occurrences = 0,
                SequenceIndex = 0,
                Start = 0,
                Successions = 0,
            };

            return (loop, loop with
            {
                Progression = movements.Slice(Random.Shared.Next(0, length), length)
            });
        }

        var loops = Enumerable.Repeat(0, 1000).Select(_ => NewLoop()).ToList();

        var minNormalizationShift = int.MaxValue;
        var maxNormalizationShift = int.MinValue;
        foreach (var (loop1, loop2) in loops)
        {
            var progression1 = loop1.GetNormalizedProgression(out var normalizationShift);
            minNormalizationShift = Math.Min(minNormalizationShift, normalizationShift);
            maxNormalizationShift = Math.Max(maxNormalizationShift, normalizationShift);

            var progression2 = loop2.GetNormalizedProgression(out normalizationShift);
            minNormalizationShift = Math.Min(minNormalizationShift, normalizationShift);
            maxNormalizationShift = Math.Max(maxNormalizationShift, normalizationShift);
            var serialized1 = Loop.Serialize(progression1);
            var serialized2 = Loop.Serialize(progression2);
            if (serialized1 != serialized2)
            {
                logger.LogInformation(string.Join(", ", MemoryMarshal.ToEnumerable(loop1.Progression).Select(x => x.RootDelta)));
                logger.LogInformation(string.Join(", ", MemoryMarshal.ToEnumerable(loop2.Progression).Select(x => x.RootDelta)));
                logger.LogInformation(string.Join(", ", MemoryMarshal.ToEnumerable(progression1).Select(x => x.RootDelta)));
                logger.LogInformation(string.Join(", ", MemoryMarshal.ToEnumerable(progression2).Select(x => x.RootDelta)));
                Assert.Fail();
            }
        }

        // these assertions may very rarely fail, when an edge case never happens in 1000 repetitions. Almost impossible.
        Assert.Equal(0, minNormalizationShift);
        Assert.Equal(length - 1, maxNormalizationShift);
    }
}