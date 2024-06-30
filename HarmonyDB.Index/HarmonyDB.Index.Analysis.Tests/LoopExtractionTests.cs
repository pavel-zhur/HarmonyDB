using System.Runtime.InteropServices;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using Microsoft.Extensions.Logging;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Tests;

public class LoopExtractionTests(ILogger<LoopExtractionTests> logger)
{
    [Fact]
    public void NoneShort()
    {
        var sequence = new[] { 1 }.Select(d => new CompactHarmonyMovement
        {
            RootDelta = (byte)d,
            ToType = ChordType.Augmented,
            FromType = ChordType.Augmented,
        }).ToArray();

        var firstRoot = (byte)Random.Shared.Next(0, 12);
        var loops = FindSimpleLoops(sequence, firstRoot);
        Trace(sequence, firstRoot, loops);

        Assert.Empty(loops);
    }

    [Fact]
    public void NoneLong()
    {
        var sequence = Enumerable.Repeat(7, 11).Select(d => new CompactHarmonyMovement
            {
                RootDelta = (byte)d,
                ToType = ChordType.Augmented,
                FromType = ChordType.Augmented,
            }).ToArray();

        var firstRoot = (byte)Random.Shared.Next(0, 12);
        var loops = FindSimpleLoops(sequence, firstRoot);
        Trace(sequence, firstRoot, loops);

        Assert.Empty(loops);
    }

    [Fact]
    public void NoneLongWithChordTypes()
    {
        var index = 0;
        ChordType GetFromType() => index == 0 ? ChordType.Augmented : (ChordType)((index - 1) / 12);
        ChordType GetToType() => (ChordType)(index++ / 12);

        var sequence = Enumerable.Repeat(7, 12 * 8 - 1).Select(d => new CompactHarmonyMovement
        {
            RootDelta = (byte)d,
            FromType = GetFromType(),
            ToType = GetToType(),
        }).ToArray();

        var loops = FindSimpleLoops(sequence, 0);
        Trace(sequence, 0, loops);

        Assert.Empty(loops);
    }

    [Fact]
    public void OneLongWithChordTypes()
    {
        var index = 0;
        ChordType GetFromType() => index == 0 ? ChordType.Augmented : (ChordType)((index - 1) / 12);
        ChordType GetToType() => (ChordType)(index++ / 12);

        var sequence = Enumerable.Repeat(7, 12 * 8).Select(d => new CompactHarmonyMovement
        {
            RootDelta = (byte)d,
            FromType = GetFromType(),
            ToType = GetToType(),
        }).ToArray();

        var loops = FindSimpleLoops(sequence, 0);
        Trace(sequence, 0, loops);

        Assert.Single(loops);
        Assert.Equal((0, 12 * 8 - 1), loops[0].SelectSingle(x => (x.StartIndex, x.EndIndex)));
    }

    [Fact]
    public void OneLong()
    {
        var sequence = Enumerable.Repeat(7, 12).Select(d => new CompactHarmonyMovement
        {
            RootDelta = (byte)d,
            ToType = ChordType.Augmented,
            FromType = ChordType.Augmented,
        }).ToArray();

        var loops = FindSimpleLoops(sequence, 0);
        Trace(sequence, 0, loops);

        Assert.Single(loops);
        Assert.Equal((0, 11), loops[0].SelectSingle(x => (x.StartIndex, x.EndIndex)));
    }

    [Fact]
    public void EndingPlusOne()
    {
        var sequence = Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12)).Concat([5, 7, 5, 7, 1]).Select(
            d => new CompactHarmonyMovement
            {
                RootDelta = (byte)d,
                ToType = ChordType.Augmented,
                FromType = ChordType.Augmented,
            }).ToArray();

        var firstRoot = (byte)Random.Shared.Next(0, 12);
        var loops = FindSimpleLoops(sequence, firstRoot);
        Trace(sequence, firstRoot, loops);

        Assert.Equal(103, loops[^1].EndIndex);
        Assert.Equal(2, loops[^1].SelectSingle(x => x.LoopLength));
    }

    [Fact]
    public void Ending()
    {
        var sequence = Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12)).Concat([5, 7, 5, 7]).Select(
            d => new CompactHarmonyMovement
            {
                RootDelta = (byte)d,
                ToType = ChordType.Augmented,
                FromType = ChordType.Augmented,
            }).ToArray();

        var firstRoot = (byte)Random.Shared.Next(0, 12);
        var loops = FindSimpleLoops(sequence, firstRoot);
        Trace(sequence, firstRoot, loops);

        Assert.Equal(103, loops[^1].EndIndex);
        Assert.Equal(2, loops[^1].SelectSingle(x => x.LoopLength));
    }

    [Fact]
    public void Beginning()
    {
        var sequence = new[] { 5, 7, 5, 7, 5 }.Concat(Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12)))
            .Select(
                d => new CompactHarmonyMovement
                {
                    RootDelta = (byte)d,
                    ToType = ChordType.Augmented,
                    FromType = ChordType.Augmented,
                }).ToArray();

        var firstRoot = (byte)Random.Shared.Next(0, 12);
        var loops = FindSimpleLoops(sequence, firstRoot);
        Trace(sequence, firstRoot, loops);

        Assert.Equal(0, loops[0].StartIndex);
        Assert.Equal(2, loops[0].SelectSingle(x => x.LoopLength));
        Assert.True(loops[0].EndIndex >= 4);
    }

    [Fact]
    public void BeginningPlusOne()
    {
        var sequence = new[] { 1, 5, 7, 5, 7, 5 }
            .Concat(Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12))).Select(
                d => new CompactHarmonyMovement
                {
                    RootDelta = (byte)d,
                    ToType = ChordType.Augmented,
                    FromType = ChordType.Augmented,
                }).ToArray();

        var loops = FindSimpleLoops(sequence, 0);
        Trace(sequence, 0, loops);

        Assert.Equal(1, loops[0].StartIndex);
        Assert.Equal(2, loops[0].SelectSingle(x => x.LoopLength));
        Assert.True(loops[0].EndIndex >= 5);
    }

    [Fact]
    public void Random100X100()
    {
        for (var i = 0; i < 100; i++)
        {
            var sequence = Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12)).Select(
                d => new CompactHarmonyMovement
                {
                    RootDelta = (byte)d,
                    ToType = ChordType.Augmented,
                    FromType = ChordType.Augmented,
                }).ToArray();

            var firstRoot = (byte)Random.Shared.Next(0, 12);
            var loops = FindSimpleLoops(sequence, firstRoot);
            Trace(sequence, firstRoot, loops);
        }
    }

    [Fact]
    public void GapLong()
    {
        var sequence = new[]
        {
            2, 2, 1, 1, 2, 2, 1, 1,
            2, 2, 1, 1, 2, 2, 1, 1,
            3, 4, 6,
            8, 4, 8, 4,

        }.Select(
            d => new CompactHarmonyMovement
            {
                RootDelta = (byte)d,
                ToType = ChordType.Augmented,
                FromType = ChordType.Augmented,
            }).ToArray();

        var loops = FindSimpleLoops(sequence, 0);
        Trace(sequence, 0, loops);

        Assert.Equal(2, loops.Count);
        Assert.True(loops[1].StartIndex - 1 > loops[0].EndIndex);
    }

    [Fact]
    public void GapImmediate()
    {
        var sequence = new[]
        {
            2, 2, 1, 1, 2, 2, 1, 1,
            2, 2, 1, 1, 2, 2, 1, 1,
            13,
            8, 4, 8, 4,

        }.Select(
            d => new CompactHarmonyMovement
            {
                RootDelta = (byte)d,
                ToType = ChordType.Augmented,
                FromType = ChordType.Augmented,
            }).ToArray();

        var loops = FindSimpleLoops(sequence, 0);
        Trace(sequence, 0, loops);

        Assert.Equal(2, loops.Count);
        Assert.True(loops[1].StartIndex - 1 > loops[0].EndIndex);
    }

    [Fact]
    public void Overlap()
    {
        var sequence = new[]
        {
            2, 2, 1, 1, 2, 2, 1, 1,
            2, 2, 1, 1, 2, 2, 1, 1,
            2, 2, 1, 1, 1, 2, 2, 1,
            2, 2, 1, 1, 1, 2, 2, 1,
            2, 2, 1, 1, 2, 1, 2, 1,
            2, 2, 1, 1, 2, 1, 2, 1,
        }.Select(
            d => new CompactHarmonyMovement
            {
                RootDelta = (byte)d,
                ToType = ChordType.Augmented,
                FromType = ChordType.Augmented,
            }).ToArray();

        var loops = FindSimpleLoops(sequence, 0);
        Trace(sequence, 0, loops);

        Assert.Equal(3, loops.Count);
        Assert.True(loops[1].StartIndex < loops[0].EndIndex);
        Assert.True(loops[2].StartIndex < loops[1].EndIndex);
    }

    [Fact]
    public void SelfJump()
    {
        var sequence = new[]
        {
            3, 3, 6,
            3, 3, 6,
            3, 9,
            3, 3, 6,
            3, 3, 6,
        }.Select(
            d => new CompactHarmonyMovement
            {
                RootDelta = (byte)d,
                ToType = ChordType.Augmented,
                FromType = ChordType.Augmented,
            }).ToArray();

        var loops = FindSimpleLoops(sequence, 0);
        Trace(sequence, 0, loops);

        Assert.Equal(3, loops.Count);
        Assert.Equal(loops[0].Normalized, loops[2].Normalized);
    }

    [Fact]
    public void SelfJumpLong()
    {
        var sequence = new[]
        {
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6,
            1, 2, 9,
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6,
        }.Select(
            d => new CompactHarmonyMovement
            {
                RootDelta = (byte)d,
                ToType = ChordType.Augmented,
                FromType = ChordType.Augmented,
            }).ToArray();

        var loops = FindSimpleLoops(sequence, 0);
        Trace(sequence, 0, loops);

        Assert.Equal(3, loops.Count);
        Assert.Equal(loops[0].Normalized, loops[2].Normalized);
    }

    [Fact]
    public void SelfJumpWithModulationWithOverlap()
    {
        var sequence = new[]
        {
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6,
            1, 2,
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6,
        }.Select(
            d => new CompactHarmonyMovement
            {
                RootDelta = (byte)d,
                ToType = ChordType.Augmented,
                FromType = ChordType.Augmented,
            }).ToArray();

        var loops = FindSimpleLoops(sequence, 0);
        Trace(sequence, 0, loops);

        Assert.Equal(2, loops.Count);
        Assert.Equal(loops[0].Normalized, loops[1].Normalized);
    }

    [Fact]
    public void SelfJumpWithModulationWithJoint()
    {
        var sequence = new[]
        {
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6,
            1, 10,
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6,
        }.Select(
            d => new CompactHarmonyMovement
            {
                RootDelta = (byte)d,
                ToType = ChordType.Augmented,
                FromType = ChordType.Augmented,
            }).ToArray();

        var loops = FindSimpleLoops(sequence, 0);
        Trace(sequence, 0, loops);

        Assert.Equal(3, loops.Count);
        Assert.Equal(loops[0].Normalized, loops[2].Normalized);
    }

    [Fact]
    public void Random100X100WithChordTypes()
    {
        for (var i = 0; i < 100; i++)
        {
            ChordType GetRandomChordType() => (ChordType)Random.Shared.Next((int)ChordType.Major, (int)ChordType.Augmented + 1);
            var chordType = GetRandomChordType();
            var sequence = Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12)).Select(
                d => new CompactHarmonyMovement
                {
                    RootDelta = (byte)d,
                    FromType = chordType,
                    ToType = chordType = GetRandomChordType(),
                }).ToArray();

            var firstRoot = (byte)Random.Shared.Next(0, 12);
            var loops = FindSimpleLoops(sequence, firstRoot);
            Trace(sequence, firstRoot, loops);
        }
    }

    private void Trace(
        ReadOnlyMemory<CompactHarmonyMovement> sequence,
        byte firstRoot,
        List<LoopBlock> loops)
    {
        var roots = CreateRoots(sequence, firstRoot);
        logger.LogInformation($"roots: {string.Join(" ", roots)}");
        foreach (var loop in loops)
        {
            var endMovement = loop.StartIndex + loop.LoopLength - 1;
            Assert.Equal(loop.LoopLength, endMovement - loop.StartIndex + 1);
            Assert.True(endMovement <= loop.EndIndex);
            Assert.InRange(loop.StartIndex, 0, sequence.Length - 1);
            Assert.InRange(endMovement, 0, sequence.Length - 1);
            Assert.InRange(loop.EndIndex, 0, sequence.Length - 1);
            Assert.Equal((loop.Normalized, loop.NormalizationShift), (Loop.Serialize(Loop.GetNormalizedProgression(loop.Loop, out var shift, out _)), shift));
            
            var rootsTrace = string.Join(" ", Enumerable.Range(loop.StartIndex, loop.EndIndex - loop.StartIndex + 1).Select(i => roots[i + 1]).Prepend(roots[loop.StartIndex]));
            var normalized = Loop.Deserialize(loop.Normalized);
            var normalizedRoots = CreateRoots(normalized, loop.NormalizationStartRoot);
            var normalizedRecreation = string.Join(" ", Enumerable.Range(0, loop.EndIndex - loop.StartIndex + 1)
                .Prepend(-1 + normalized.Length)
                .Select(x => x - loop.NormalizationShift + normalized.Length)
                .Select(i => normalizedRoots[(i % normalized.Length) + 1]));

            Assert.Equal(rootsTrace, normalizedRecreation);

            logger.LogInformation($"found {loop.LoopLength}" +
                                  $" + {loop.EndIndex - endMovement}" +
                                  $" ({loop.StartIndex}..{endMovement}..{loop.EndIndex}):" +
                                  $" {rootsTrace};");
        }
    }

    private List<byte> CreateRoots(ReadOnlyMemory<CompactHarmonyMovement> sequence, byte firstRoot)
    {
        var roots = firstRoot
            .Once()
            .Concat(MemoryMarshal.ToEnumerable(sequence)
                .Select(d => firstRoot = Note.Normalize(d.RootDelta + firstRoot)))
            .ToList();

        return roots;
    }

    private List<LoopBlock> FindSimpleLoops(ReadOnlyMemory<CompactHarmonyMovement> sequence, byte firstRoot)
    {
        var roots = CreateRoots(sequence, firstRoot);

        Dictionary<(byte root, ChordType chordType), int> indices = new()
        {
            { (roots[0], sequence.Span[0].FromType), -1 }
        };

        var loops = new List<LoopBlock>();

        var movementIndex = 0;
        while (movementIndex < sequence.Length)
        {
            var key = (roots[movementIndex + 1], sequence.Span[movementIndex].ToType);
            if (indices.TryAdd(key, movementIndex))
            {
                movementIndex++;
                continue;
            }

            var movementIndexToLoopStart = indices[key];
            var loopStartRoot = roots[movementIndexToLoopStart + 1];
            Assert.Equal(loopStartRoot, roots[movementIndex + 1]);
            Assert.Equal(
                movementIndexToLoopStart == -1 ? sequence.Span[0].FromType : sequence.Span[movementIndexToLoopStart].ToType, 
                sequence.Span[movementIndex].ToType);

            var foundLoop = sequence.Slice(movementIndexToLoopStart + 1, movementIndex - movementIndexToLoopStart);
            var movementIndexInLoop = 0;
            movementIndex++;
            while (movementIndex <= sequence.Length)
            {
                if (movementIndex < sequence.Length
                    && sequence.Span[movementIndex].Equals(foundLoop.Span[movementIndexInLoop]))
                {
                    movementIndexInLoop = (movementIndexInLoop + 1) % foundLoop.Length;
                    movementIndex++;
                    continue;
                }

                break;
            }

            movementIndex--; // now points to the movement leading to the last root of the found sequence

            var normalized = Loop.Serialize(Loop.GetNormalizedProgression(foundLoop, out var normalizationShift, out _));
            loops.Add(new()
            {
                Loop = foundLoop,
                Normalized = normalized,
                NormalizationShift = normalizationShift,
                NormalizationStartRoot = roots[movementIndexToLoopStart + 1 + normalizationShift],
                StartIndex = movementIndexToLoopStart + 1,
                EndIndex = movementIndex,
            });

            movementIndex -= foundLoop.Length - 1;
            indices.Clear();
        }

        return loops;
    }

    public record struct LoopBlock
    {
        public required ReadOnlyMemory<CompactHarmonyMovement> Loop { get; init; }

        public int LoopLength => Loop.Length;

        public required string Normalized { get; init; }

        public required int NormalizationShift { get; init; }

        public required byte NormalizationStartRoot { get; init; }

        public required int StartIndex { get; init; }

        public required int EndIndex { get; init; }
    }
}