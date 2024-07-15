using System.Runtime.InteropServices;
using System.Xml.Serialization;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.Analysis.Tools;
using Microsoft.Extensions.Logging;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Tests;

public class LoopNormalizationTests(ILogger<LoopNormalizationTests> logger, IndexExtractor indexExtractor)
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void NormalizationIsCorrectForMultipleOptions(int repetitions)
    {
        var minNormalizationShift = int.MaxValue;
        var maxNormalizationShift = int.MinValue;
        var minInvertedShift = int.MaxValue;
        var maxInvertedShift = int.MinValue;
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

            var loop = movements;

            indexExtractor.GetNormalizedProgression(loop, out var normalizationShift, out var invariants);
            var invertedShift = indexExtractor.InvertNormalizationShift(normalizationShift, loop.Length);
            minNormalizationShift = Math.Min(minNormalizationShift, normalizationShift);
            maxNormalizationShift = Math.Max(maxNormalizationShift, normalizationShift);
            minInvertedShift = Math.Min(minInvertedShift, invertedShift);
            maxInvertedShift = Math.Max(maxInvertedShift, invertedShift);
            Assert.Equal(repetitions, invariants);
        }

        // these assertions may very rarely fail, when an edge case never happens in 1000 repetitions. Almost impossible.
        Assert.Equal(0, minNormalizationShift);
        Assert.Equal(0, minInvertedShift);
        Assert.Equal(length - 1, maxNormalizationShift);
        Assert.Equal(length * repetitions - 1, maxInvertedShift);
    }

    [Fact]
    public void NormalizedIsStable1000()
    {
        const int length = 10;
        (ReadOnlyMemory<CompactHarmonyMovement> loop1, ReadOnlyMemory<CompactHarmonyMovement> loop2) NewLoop()
        {
            ReadOnlyMemory<CompactHarmonyMovement> movements = Enumerable.Range(0, length).Select(_ => new CompactHarmonyMovement
            {
                ToType = (ChordType)Random.Shared.Next(0, 8),
                FromType = (ChordType)Random.Shared.Next(0, 8),
                RootDelta = (byte)Random.Shared.Next(0, 12),
            }).ToList().SelectSingle(x => x.Concat(x)).ToArray();

            var loop = movements.Slice(0, length);

            return (loop, movements.Slice(Random.Shared.Next(0, length), length));
        }

        var loops = Enumerable.Repeat(0, 1000).Select(_ => NewLoop()).ToList();

        var minNormalizationShift = int.MaxValue;
        var maxNormalizationShift = int.MinValue;
        var minInvertedShift = int.MaxValue;
        var maxInvertedShift = int.MinValue;
        foreach (var (loop1, loop2) in loops)
        {
            var progression1 = indexExtractor.GetNormalizedProgression(loop1, out var normalizationShift);
            var invertedShift = indexExtractor.InvertNormalizationShift(normalizationShift, progression1.Length);
            minNormalizationShift = Math.Min(minNormalizationShift, normalizationShift);
            maxNormalizationShift = Math.Max(maxNormalizationShift, normalizationShift);
            minInvertedShift = Math.Min(minInvertedShift, invertedShift);
            maxInvertedShift = Math.Max(maxInvertedShift, invertedShift);

            var progression2 = indexExtractor.GetNormalizedProgression(loop2, out normalizationShift);
            invertedShift = indexExtractor.InvertNormalizationShift(normalizationShift, progression1.Length);
            minNormalizationShift = Math.Min(minNormalizationShift, normalizationShift);
            maxNormalizationShift = Math.Max(maxNormalizationShift, normalizationShift);
            minInvertedShift = Math.Min(minInvertedShift, invertedShift);
            maxInvertedShift = Math.Max(maxInvertedShift, invertedShift);
            var serialized1 = progression1.SerializeLoop();
            var serialized2 = progression2.SerializeLoop();
            if (serialized1 != serialized2)
            {
                logger.LogInformation(string.Join(", ", MemoryMarshal.ToEnumerable(loop1).Select(x => x.RootDelta)));
                logger.LogInformation(string.Join(", ", MemoryMarshal.ToEnumerable(loop2).Select(x => x.RootDelta)));
                logger.LogInformation(string.Join(", ", MemoryMarshal.ToEnumerable(progression1).Select(x => x.RootDelta)));
                logger.LogInformation(string.Join(", ", MemoryMarshal.ToEnumerable(progression2).Select(x => x.RootDelta)));
                Assert.Fail();
            }
        }

        // these assertions may very rarely fail, when an edge case never happens in 1000 repetitions. Almost impossible.
        Assert.Equal(0, minNormalizationShift);
        Assert.Equal(0, minInvertedShift);
        Assert.Equal(length - 1, maxNormalizationShift);
        Assert.Equal(length - 1, maxInvertedShift);
    }
}