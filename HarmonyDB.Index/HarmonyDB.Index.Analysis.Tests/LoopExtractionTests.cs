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
        var sequence = new CompactHarmonyMovementsSequence
        {
            FirstRoot = 0,
            FirstMovementFromIndex = 0,
            Movements = new[] { 1 }.Select(d => new CompactHarmonyMovement
            {
                RootDelta = (byte)d,
                ToType = ChordType.Augmented,
                FromType = ChordType.Augmented,
            }).ToArray(),
        };

        var loops = FindSimpleLoops(sequence);
        Trace(sequence, loops);

        Assert.Empty(loops);
    }

    [Fact]
    public void NoneLong()
    {
        var sequence = new CompactHarmonyMovementsSequence
        {
            FirstRoot = 0,
            FirstMovementFromIndex = 0,
            Movements = Enumerable.Repeat(7, 11).Select(d => new CompactHarmonyMovement
            {
                RootDelta = (byte)d,
                ToType = ChordType.Augmented,
                FromType = ChordType.Augmented,
            }).ToArray(),
        };

        var loops = FindSimpleLoops(sequence);
        Trace(sequence, loops);

        Assert.Empty(loops);
    }

    [Fact]
    public void NoneLongWithChordTypes()
    {
        var index = 0;
        ChordType GetFromType() => index == 0 ? ChordType.Augmented : (ChordType)((index - 1) / 12);
        ChordType GetToType() => (ChordType)(index++ / 12);

        var sequence = new CompactHarmonyMovementsSequence
        {
            FirstRoot = 0,
            FirstMovementFromIndex = 0,
            Movements = Enumerable.Repeat(7, 12 * 8 - 1).Select(d => new CompactHarmonyMovement
            {
                RootDelta = (byte)d,
                FromType = GetFromType(),
                ToType = GetToType(),
            }).ToArray(),
        };

        var loops = FindSimpleLoops(sequence);
        Trace(sequence, loops);

        Assert.Empty(loops);
    }

    [Fact]
    public void OneLongWithChordTypes()
    {
        var index = 0;
        ChordType GetFromType() => index == 0 ? ChordType.Augmented : (ChordType)((index - 1) / 12);
        ChordType GetToType() => (ChordType)(index++ / 12);

        var sequence = new CompactHarmonyMovementsSequence
        {
            FirstRoot = 0,
            FirstMovementFromIndex = 0,
            Movements = Enumerable.Repeat(7, 12 * 8).Select(d => new CompactHarmonyMovement
            {
                RootDelta = (byte)d,
                FromType = GetFromType(),
                ToType = GetToType(),
            }).ToArray(),
        };

        var loops = FindSimpleLoops(sequence);
        Trace(sequence, loops);

        Assert.Single(loops);
        Assert.Equal((0, 12 * 8 - 1, 12 * 8 - 1), loops[0].SelectSingle(x => (x.start, x.endMovement, x.endPaintMovement)));
    }

    [Fact]
    public void OneLong()
    {
        var sequence = new CompactHarmonyMovementsSequence
        {
            FirstRoot = 0,
            FirstMovementFromIndex = 0,
            Movements = Enumerable.Repeat(7, 12).Select(d => new CompactHarmonyMovement
            {
                RootDelta = (byte)d,
                ToType = ChordType.Augmented,
                FromType = ChordType.Augmented,
            }).ToArray(),
        };

        var loops = FindSimpleLoops(sequence);
        Trace(sequence, loops);

        Assert.Single(loops);
        Assert.Equal((0, 11, 11), loops[0].SelectSingle(x => (x.start, x.endMovement, x.endPaintMovement)));
    }

    [Fact]
    public void EndingPlusOne()
    {
        var sequence = new CompactHarmonyMovementsSequence
        {
            FirstRoot = 0,
            FirstMovementFromIndex = 0,
            Movements = Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12)).Concat([5, 7, 5, 7, 1]).Select(d => new CompactHarmonyMovement
            {
                RootDelta = (byte)d,
                ToType = ChordType.Augmented,
                FromType = ChordType.Augmented,
            }).ToArray(),
        };

        var loops = FindSimpleLoops(sequence);
        Trace(sequence, loops);

        Assert.Equal(103, loops[^1].endPaintMovement);
        Assert.Equal(2, loops[^1].SelectSingle(x => x.sequence.Length));
    }

    [Fact]
    public void Ending()
    {
        var sequence = new CompactHarmonyMovementsSequence
            {
                FirstRoot = 0,
                FirstMovementFromIndex = 0,
                Movements = Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12)).Concat([5, 7, 5, 7]).Select(
                    d => new CompactHarmonyMovement
                    {
                        RootDelta = (byte)d,
                        ToType = ChordType.Augmented,
                        FromType = ChordType.Augmented,
                    }).ToArray(),
            };

        var loops = FindSimpleLoops(sequence);
        Trace(sequence, loops);

        Assert.Equal(103, loops[^1].endPaintMovement);
        Assert.Equal(2, loops[^1].SelectSingle(x => x.sequence.Length));
    }

    [Fact]
    public void Beginning()
    {
        var sequence = new CompactHarmonyMovementsSequence
            {
                FirstRoot = 0,
                FirstMovementFromIndex = 0,
                Movements = new[] {5, 7, 5, 7, 5}.Concat(Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12))).Select(
                    d => new CompactHarmonyMovement
                    {
                        RootDelta = (byte)d,
                        ToType = ChordType.Augmented,
                        FromType = ChordType.Augmented,
                    }).ToArray(),
            };

        var loops = FindSimpleLoops(sequence);
        Trace(sequence, loops);

        Assert.Equal(0, loops[0].start);
        Assert.Equal(2, loops[0].SelectSingle(x => x.sequence.Length));
        Assert.True(loops[0].endPaintMovement >= 4);
    }

    [Fact]
    public void BeginningPlusOne()
    {
        var sequence = new CompactHarmonyMovementsSequence
            {
                FirstRoot = 0,
                FirstMovementFromIndex = 0,
                Movements = new[] {1, 5, 7, 5, 7, 5}.Concat(Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12))).Select(
                    d => new CompactHarmonyMovement
                    {
                        RootDelta = (byte)d,
                        ToType = ChordType.Augmented,
                        FromType = ChordType.Augmented,
                    }).ToArray(),
            };

        var loops = FindSimpleLoops(sequence);
        Trace(sequence, loops);

        Assert.Equal(1, loops[0].start);
        Assert.Equal(2, loops[0].SelectSingle(x => x.sequence.Length));
        Assert.True(loops[0].endPaintMovement >= 5);
    }

    [Fact]
    public void Random100X100()
    {
        for (var i = 0; i < 100; i++)
        {
            var sequence = new CompactHarmonyMovementsSequence
            {
                FirstRoot = 0,
                FirstMovementFromIndex = 0,
                Movements = Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12)).Select(
                    d => new CompactHarmonyMovement
                    {
                        RootDelta = (byte)d,
                        ToType = ChordType.Augmented,
                        FromType = ChordType.Augmented,
                    }).ToArray(),
            };

            var loops = FindSimpleLoops(sequence);
            Trace(sequence, loops);
        }
    }

    [Fact]
    public void Gap()
    {
        var sequence = new CompactHarmonyMovementsSequence
            {
                FirstRoot = 0,
                FirstMovementFromIndex = 0,
                Movements = new[]
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
                    }).ToArray(),
            };

        var loops = FindSimpleLoops(sequence);
        Trace(sequence, loops);

        Assert.Equal(2, loops.Count);
        Assert.True(loops[1].start - 1 > loops[0].endPaintMovement);
    }

    [Fact]
    public void Overlap()
    {
        var sequence = new CompactHarmonyMovementsSequence
            {
                FirstRoot = 0,
                FirstMovementFromIndex = 0,
                Movements = new[]
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
                    }).ToArray(),
            };

        var loops = FindSimpleLoops(sequence);
        Trace(sequence, loops);

        Assert.Equal(3, loops.Count);
        Assert.True(loops[1].start < loops[0].endPaintMovement);
        Assert.True(loops[2].start < loops[1].endPaintMovement);
    }

    [Fact]
    public void SelfJump()
    {
        var sequence = new CompactHarmonyMovementsSequence
            {
                FirstRoot = 0,
                FirstMovementFromIndex = 0,
                Movements = new[]
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
                    }).ToArray(),
            };

        var loops = FindSimpleLoops(sequence);
        Trace(sequence, loops);

        Assert.Equal(3, loops.Count);
        Assert.Equal(Loop.Serialize(loops[0].sequence), Loop.Serialize(loops[2].sequence));
    }

    [Fact]
    public void SelfJumpLong()
    {
        var sequence = new CompactHarmonyMovementsSequence
            {
                FirstRoot = 0,
                FirstMovementFromIndex = 0,
                Movements = new[]
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
                    }).ToArray(),
            };

        var loops = FindSimpleLoops(sequence);
        Trace(sequence, loops);

        Assert.Equal(3, loops.Count);
        Assert.Equal(Loop.Serialize(loops[0].sequence), Loop.Serialize(loops[2].sequence));
    }

    [Fact]
    public void SelfJumpWithModulation()
    {
        var sequence = new CompactHarmonyMovementsSequence
            {
                FirstRoot = 0,
                FirstMovementFromIndex = 0,
                Movements = new[]
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
                    }).ToArray(),
            };

        var loops = FindSimpleLoops(sequence);
        Trace(sequence, loops);

        Assert.Equal(2, loops.Count);
        Assert.Equal(Loop.Serialize(loops[0].sequence), Loop.Serialize(loops[1].sequence));
    }

    [Fact]
    public void Random100X100WithChordTypes()
    {
        for (var i = 0; i < 100; i++)
        {
            ChordType GetRandomChordType() => (ChordType)Random.Shared.Next((int)ChordType.Major, (int)ChordType.Augmented + 1);
            var chordType = GetRandomChordType();
            var sequence = new CompactHarmonyMovementsSequence
            {
                FirstRoot = 0,
                FirstMovementFromIndex = 0,
                Movements = Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12)).Select(
                    d => new CompactHarmonyMovement
                    {
                        RootDelta = (byte)d,
                        ToType = chordType = GetRandomChordType(),
                        FromType = chordType,
                    }).ToArray(),
            };

            var loops = FindSimpleLoops(sequence);
            Trace(sequence, loops);
        }
    }

    private void Trace(
        CompactHarmonyMovementsSequence sequence, 
        List<(ReadOnlyMemory<CompactHarmonyMovement> sequence, int start, int endMovement, int endPaintMovement)> loops)
    {
        var roots = CreateRoots(sequence);
        logger.LogInformation($"roots: {string.Join(" ", roots)}");
        foreach (var loop in loops)
        {
            Assert.Equal(loop.sequence.Length, loop.endMovement - loop.start + 1);
            Assert.True(loop.endMovement <= loop.endPaintMovement);
            logger.LogInformation($"found {loop.sequence.Length} + {loop.endPaintMovement - loop.endMovement} ({loop.start}..{loop.endMovement}..{loop.endPaintMovement}): {string.Join(" ", Enumerable.Range(loop.start, loop.endPaintMovement - loop.start + 1).Select(i => roots[i + 1]).Prepend(roots[loop.start]))}");
        }
    }

    private List<byte> CreateRoots(CompactHarmonyMovementsSequence compactHarmonyMovementsSequence)
    {
        var nextRoot = compactHarmonyMovementsSequence.FirstRoot;
        var roots = nextRoot
            .Once()
            .Concat(MemoryMarshal.ToEnumerable(compactHarmonyMovementsSequence.Movements)
                .Select(d => nextRoot = Note.Normalize(d.RootDelta + nextRoot)))
            .ToList();

        return roots;
    }

    private List<(ReadOnlyMemory<CompactHarmonyMovement> sequence, int start, int endMovement, int endPaintMovement)> FindSimpleLoops(CompactHarmonyMovementsSequence compactHarmonyMovementsSequence)
    {
        var roots = CreateRoots(compactHarmonyMovementsSequence);
        var sequence = compactHarmonyMovementsSequence.Movements;

        Dictionary<(byte root, ChordType chordType), int> indices = new()
        {
            { (roots[0], sequence.Span[0].FromType), -1 }
        };

        var loops = new List<(ReadOnlyMemory<CompactHarmonyMovement> sequence, int start, int endMovement, int endPaintMovement)>();

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
            loops.Add((foundLoop, movementIndexToLoopStart + 1, movementIndexToLoopStart + foundLoop.Length, movementIndex));

            movementIndex -= foundLoop.Length - 1;
            indices.Clear();
        }

        return loops;
    }
}