using System.Runtime.InteropServices;
using System.Text;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Index;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.Analysis.Tools;
using Microsoft.Extensions.Logging;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Tests;

public class LoopExtractionTests(ILogger<LoopExtractionTests> logger, ChordDataParser chordDataParser, ProgressionsBuilder progressionsBuilder, IndexExtractor indexExtractor, ProgressionsVisualizer progressionsVisualizer)
{
    [InlineData("Am D G E Am D G E Am D G E Am D G E", 5)] // normalized start of Am-D-G-E is D.
    [InlineData("Gm C F D Gm C F D Gm C F D Gm C F D", 3)]
    [InlineData("Bb G E Am D G E Am D G E Am D G E", 5)]
    [InlineData("Bb Am D G E Am D G E Am D G E Am D G E", 5)]
    [InlineData("F# Gm C F D Gm C F D Gm C F D Gm C F D", 3)]
    [Theory]
    public void NormalizationRootInterpretationCorrect(string chordsInput, byte expectedNormalizedRoot)
    {
        var (sequence, firstRoot) = InputChords(chordsInput);
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);

        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(expectedNormalizedRoot, loops.Single().NormalizationRoot);
    }

    [Fact]
    public async Task HopeNoAmbiguousSelfJumps()
    {
        var source = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var counters = await Task.WhenAll(Enumerable.Range(0, 3).Select(async i =>
        {
            await Task.Yield();
            return Worker(i, source.Token);
        }));
        logger.LogInformation(string.Join(", ", counters));

        int Worker(int i, CancellationToken cancellationToken)
        {
            var counter = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                counter++;
                ChordType last = ChordType.Major;
                ChordType GetRandomChordType()
                {
                    while (true)
                    {
                        var next = (ChordType)Random.Shared.Next((int)ChordType.Major, (int)ChordType.Augmented - 3);
                        if (next != last)
                        {
                            last = next;
                            return next;
                        }
                    }
                }

                var chordType = GetRandomChordType();
                var sequence = Enumerable.Range(0, 20).Select(_ => Random.Shared.Next(0, 12)).Select(
                    d => new CompactHarmonyMovement
                    {
                        RootDelta = (byte)d,
                        FromType = chordType,
                        ToType = chordType = GetRandomChordType(),
                    }).ToArray();

                var firstRoot = (byte)Random.Shared.Next(0, 12);
                var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
                var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);

                if (loopSelfJumpsBlocks.Count > 1)
                {
                    logger.LogInformation($"{i}: {loopSelfJumpsBlocks.Count}");
                    if (loopSelfJumpsBlocks
                        .SelectMany((x, i) => x.ChildLoops.Select(x => (x.StartIndex, x.EndIndex)))
                        .AnyDuplicates())
                    {
                        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);
                        Assert.Fail("Found!");
                    }
                }
            }

            return counter;
        }
    }
    
    [Fact]
    public void SelfJumpsDifferentDeltasNotFound()
    {
        var (sequence, firstRoot) = InputChords("C A C D C A C D C A C D C A C");
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);

        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);
        Assert.Empty(loopSelfJumpsBlocks);
    }
    
    [Fact]
    public void SelfJumpsSameDeltaNoAmbiguity()
    {
        var (sequence, firstRoot) = InputChords(
            "C F C G C F C G C F C G C F C G C F C G C A#m" +
            " C G C F C G C F C G C F C G C F C G C F C G C Bm" +
            " F C G C F C G C F C G C F C G C F C G C Bm" +
            " G C F C G C F C G C F C G C F C G C F C G C");
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);

        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);
        Assert.Equal(4, loopSelfJumpsBlocks.Count);

        // important: same normalization
        Assert.Single(loopSelfJumpsBlocks.Select(x => x.Normalized).Distinct());
    }
    
    [Fact]
    public void NoneShort()
    {
        var (sequence, firstRoot) = InputDeltas(1);
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);

        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Empty(loops);
    }

    [Fact]
    public void NoneLong()
    {
        var (sequence, firstRoot) = InputDeltas(Enumerable.Repeat(7, 11));
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

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

        byte firstRoot = 0;
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

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

        byte firstRoot = 0;
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Single(loops);
        Assert.Equal((0, 12 * 8 - 1), loops[0].SelectSingle(x => (x.StartIndex, x.EndIndex)));
    }

    [Fact]
    public void OneLong()
    {
        var (sequence, firstRoot) = InputDeltas(Enumerable.Repeat(7, 12), 0);
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Single(loops);
        Assert.Equal((0, 11), loops[0].SelectSingle(x => (x.StartIndex, x.EndIndex)));
    }

    [Fact]
    public void EndingPlusOne()
    {
        var (sequence, firstRoot) = InputDeltas(Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12)).Concat([5, 7, 5, 7, 1]));
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(103, loops[^1].EndIndex);
        Assert.Equal(2, loops[^1].SelectSingle(x => x.LoopLength));
    }

    [Fact]
    public void Ending()
    {
        var (sequence, firstRoot) = InputDeltas(Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12)).Concat([5, 7, 5, 7]));
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(103, loops[^1].EndIndex);
        Assert.Equal(2, loops[^1].SelectSingle(x => x.LoopLength));
    }

    [Fact]
    public void Beginning()
    {
        var (sequence, firstRoot) = InputDeltas(new[] { 5, 7, 5, 7, 5 }.Concat(Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12))));
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(0, loops[0].StartIndex);
        Assert.Equal(2, loops[0].SelectSingle(x => x.LoopLength));
        Assert.True(loops[0].EndIndex >= 4);
    }

    [Fact]
    public void BeginningPlusOne()
    {
        var (sequence, firstRoot) = InputDeltas(
            new[] { 1, 5, 7, 5, 7, 5 }
            .Concat(Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12))), 
            0);

        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(1, loops[0].StartIndex);
        Assert.Equal(2, loops[0].SelectSingle(x => x.LoopLength));
        Assert.True(loops[0].EndIndex >= 5);
    }

    [Fact]
    public void Random100X100()
    {
        for (var i = 0; i < 100; i++)
        {
            var (sequence, firstRoot) = InputDeltas(Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12)));
            var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
            var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
            TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks, false);
        }
    }

    [Fact]
    public void ChordsInputWithAlterations()
    {
        var sequenceChords = "C# F# C# F# C# G# C# G# C# G# C# G# F# C# F# C# F# C# F#";
        var (sequence, firstRoot) = InputChords(sequenceChords);
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Single(loopSelfJumpsBlocks);
        Assert.Equal(2, loopSelfJumpsBlocks.Single().ChildJumps.Count);
        Assert.Equal(4, loopSelfJumpsBlocks.Single().ChildLoops.Count);
    }

    [Fact]
    public void GapLong()
    {
        var (sequence, firstRoot) = InputDeltas(
            2, 2, 1, 1, 2, 2, 1, 1,
            2, 2, 1, 1, 2, 2, 1, 1,
            3, 4, 6,
            8, 4, 8, 4);

        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(2, loops.Count);
        Assert.True(loops[1].StartIndex - 1 > loops[0].EndIndex);
    }

    [Fact]
    public void GapImmediate()
    {
        var (sequence, firstRoot) = InputDeltas(
            2, 2, 1, 1, 2, 2, 1, 1,
            2, 2, 1, 1, 2, 2, 1, 1,
            13,
            8, 4, 8, 4);

        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(2, loops.Count);
        Assert.True(loops[1].StartIndex - 1 > loops[0].EndIndex);
    }

    [Fact]
    public void Overlap()
    {
        var (sequence, firstRoot) = InputDeltas(
            2, 2, 1, 1, 2, 2, 1, 1,
            2, 2, 1, 1, 2, 2, 1, 1,
            2, 2, 1, 1, 1, 2, 2, 1,
            2, 2, 1, 1, 1, 2, 2, 1,
            2, 2, 1, 1, 2, 1, 2, 1,
            2, 2, 1, 1, 2, 1, 2, 1);

        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(3, loops.Count);
        Assert.True(loops[1].StartIndex < loops[0].EndIndex);
        Assert.True(loops[2].StartIndex < loops[1].EndIndex);
    }

    [Theory]
    // F E Am Dm
    [InlineData(0, 7, 2, 1, "Am Dm F E Am Dm F E Am E Am Dm F E Am Dm F E Am")]
    [InlineData(0, 8, 2, 0, "Am Dm F E Am Dm F E Am F E Am Dm F E Am Dm F E Am")]
    [InlineData(5, 7, 3, 1, "Am Dm F E Am Dm F E Am Dm E Am Dm F E Am Dm F E Am")]
    [InlineData(5, 0, 3, 2, "Am Dm F E Am Dm F E Am Dm Am Dm F E Am Dm F E Am")]
    [InlineData(8, 0, 0, 2, "Am Dm F E Am Dm F E Am Dm F Am Dm F E Am Dm F E Am")]
    [InlineData(8, 5, 0, 3, "Am Dm F E Am Dm F E Am Dm F Dm F E Am Dm F E Am")]
    [InlineData(7, 5, 1, 3, "Am Dm F E Am Dm F E Am Dm F E Dm F E Am Dm F E Am")]
    [InlineData(7, 8, 1, 0, "Am Dm F E Am Dm F E Am Dm F E F E Am Dm F E Am")]
    [InlineData(8, 9, 1, 0, "A#m D#m F# E# A#m D#m F# E# A#m D#m F# E# F# E# A#m D#m F# E# A#m")]
    public void SelfJumpSimple(
        byte fromNormalizationRoot, byte toNormalizationRoot,
        byte rootIndex1, byte rootIndex2,
        string sequenceChords)
    {
        var (sequence, firstRoot) = InputChords(sequenceChords);
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Single(loopSelfJumpsBlocks);
        var loopSelfMultiJumpBlock = loopSelfJumpsBlocks.Single();

        Assert.Single(loopSelfMultiJumpBlock.ChildJumps);
        var loopSelfJumpBlock = loopSelfMultiJumpBlock.ChildJumps.Single();

        Assert.NotNull(loopSelfJumpBlock.JointLoop);
        Assert.NotNull(loopSelfJumpBlock.JointMovement);
        Assert.False(loopSelfMultiJumpBlock.HasModulations);
        Assert.False(loopSelfMultiJumpBlock.IsModulation);
        Assert.Single(loopSelfMultiJumpBlock.NormalizationRootsFlow);
        Assert.Equal(LoopSelfJumpType.SameKeyJoint, loopSelfJumpBlock.Type);
        
        AssertNormalized((fromNormalizationRoot, toNormalizationRoot), (rootIndex1, rootIndex2), loopSelfJumpBlock);
    }

    private (byte root1, int root2) ToNormalizedRoots((int rootIndex1, int rootIndex2) rootIndices, string normalized,
        byte normalizationRoot1, byte? normalizationRoot2 = null)
        => normalized.DeserializeLoop()
            .SelectSingle(s => (s, roots: indexExtractor.CreateRoots(s, normalizationRoot1)))
            .SelectSingle(r =>
                normalizationRoot2 == null
                    ? (roots1: r.roots, roots2: r.roots)
                    : (roots1: r.roots, roots2: indexExtractor.CreateRoots(r.s, normalizationRoot2.Value)))
            .SelectSingle(r => (r.roots1[rootIndices.rootIndex1], r.roots2[rootIndices.rootIndex2]));

    [Fact]
    public void SelfJumpMultiModulationOneJointOneAmbiguous()
    {
        //                    1 4 1 4 1               4 1 4 1 4 1 4
        //                            4 1 4 1 4 1 4 1
        var sequenceChords = "C F C F C G C G C G C G F C F C F C F";
        var (sequence, firstRoot) = InputChords(sequenceChords);
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Single(loopSelfJumpsBlocks);
        Assert.Equal(2, loopSelfJumpsBlocks.Single().ChildJumps.Count);
        Assert.Equal(4, loopSelfJumpsBlocks.Single().ChildLoops.Count);
        Assert.True(loopSelfJumpsBlocks.Single().ChildJumps.All(x => x.IsModulation));
        Assert.True(loopSelfJumpsBlocks.Single().HasModulations);
        Assert.False(loopSelfJumpsBlocks.Single().IsModulation);
        Assert.Equal(3, loopSelfJumpsBlocks.Single().NormalizationRootsFlow.Count);
        Assert.Equal(loopSelfJumpsBlocks.Single().NormalizationRootsFlow[0], loopSelfJumpsBlocks.Single().NormalizationRootsFlow[2]);

        Assert.True(loopSelfJumpsBlocks.Single().ChildJumps[0].SelectSingle(x => x.JointLoop == null));
        Assert.True(loopSelfJumpsBlocks.Single().ChildJumps[0].SelectSingle(x => x.JointMovement == null));
        Assert.Equal(LoopSelfJumpType.ModulationAmbiguousChord, loopSelfJumpsBlocks.Single().ChildJumps[0].Type);

        AssertNormalized((3, 3), (0, 1), loopSelfJumpsBlocks.Single().ChildJumps[0]);

        Assert.True(loopSelfJumpsBlocks.Single().ChildJumps[1].SelectSingle(x => x.JointLoop != null));
        Assert.True(loopSelfJumpsBlocks.Single().ChildJumps[1].SelectSingle(x => x.JointMovement != null));
        Assert.Equal(LoopSelfJumpType.ModulationJointMovement, loopSelfJumpsBlocks.Single().ChildJumps[1].Type);

        AssertNormalized((10, 8), (0, 1), loopSelfJumpsBlocks.Single().ChildJumps[1]);
    }

    [Theory]

    //                                                         1 4 5 6 1 4 5 6 1 4
    //                                                                           6 1  4  5  6 1  4  5  6
    [InlineData(8, 0, 2, false, "C F G A C F G A C F G# C# D# F G# C# D# F")]

    //                                                         1 4 5 6 1 4 5 6 1 4
    //                                                                           1  4  5 6 1 4  5 6
    [InlineData(8, 0, 3, true, "C F G A C F G A C F  A# C D F A# C D")]
    public void SelfJumpModulationAmbiguousWithAndWithoutJointLoop(
        byte root,
        int rootIndex1, int rootIndex2, 
        bool hasJointLoop,
        string sequenceChords)
    {
        var (sequence, firstRoot) = InputChords(sequenceChords);
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Single(loopSelfJumpsBlocks);
        Assert.Single(loopSelfJumpsBlocks.Single().ChildJumps);
        Assert.Equal(hasJointLoop ? 3 : 2, loopSelfJumpsBlocks.Single().ChildLoops.Count);
        Assert.True(loopSelfJumpsBlocks.Single().ChildJumps.All(x => x.IsModulation));
        Assert.True(loopSelfJumpsBlocks.Single().HasModulations);
        Assert.True(loopSelfJumpsBlocks.Single().IsModulation);
        Assert.Equal(2, loopSelfJumpsBlocks.Single().NormalizationRootsFlow.Count);

        Assert.Equal(hasJointLoop, loopSelfJumpsBlocks.Single().ChildJumps[0].JointLoop != null);
        Assert.True(loopSelfJumpsBlocks.Single().ChildJumps[0].JointMovement == null);
        Assert.Equal(LoopSelfJumpType.ModulationAmbiguousChord, loopSelfJumpsBlocks.Single().ChildJumps[0].Type);

        AssertNormalized((root, root), (rootIndex1, rootIndex2), loopSelfJumpsBlocks.Single().ChildJumps[0]);
    }

    [Fact]
    public void SelfJumpMultiModulationWithJointMovements()
    {
        var sequenceChords = "C F C F D G D G D G A E A E A E";
        var (sequence, firstRoot) = InputChords(sequenceChords);
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Single(loopSelfJumpsBlocks);
        Assert.Equal(2, loopSelfJumpsBlocks.Single().ChildJumps.Count);
        Assert.Equal(3, loopSelfJumpsBlocks.Single().ChildLoops.Count);
        Assert.True(loopSelfJumpsBlocks.Single().ChildJumps.All(x => x.JointMovement != null));
        Assert.True(loopSelfJumpsBlocks.Single().ChildJumps.All(x => x.JointLoop == null));
        Assert.True(loopSelfJumpsBlocks.Single().HasModulations);
        Assert.True(loopSelfJumpsBlocks.Single().IsModulation);
        Assert.Equal(3, loopSelfJumpsBlocks.Single().NormalizationRootsFlow.Count);
        Assert.Equal(loopSelfJumpsBlocks.Single().NormalizationRootsFlow, loopSelfJumpsBlocks.Single().NormalizationRootsFlow.Distinct());
        Assert.Equal(LoopSelfJumpType.ModulationJointMovement, loopSelfJumpsBlocks.Single().ChildJumps[0].Type);
        Assert.Equal(LoopSelfJumpType.ModulationJointMovement, loopSelfJumpsBlocks.Single().ChildJumps[1].Type);

        AssertNormalized((8, 5), (1, 0), loopSelfJumpsBlocks.Single().ChildJumps[0]);
        AssertNormalized((10, 0), (1, 1), loopSelfJumpsBlocks.Single().ChildJumps[1]);
    }

    [Fact]
    public void SelfJumpMulti()
    {
        var sequenceChords = "Am Dm F E Am Dm F E Am E Am Dm F E Am Dm F E Am E Am Dm F E";
        var (sequence, firstRoot) = InputChords(sequenceChords);
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Single(loopSelfJumpsBlocks);
        Assert.Equal(2, loopSelfJumpsBlocks.Single().ChildJumps.Count);
        Assert.Equal(5, loopSelfJumpsBlocks.Single().ChildLoops.Count);
        Assert.True(loopSelfJumpsBlocks.Single().ChildJumps.All(x => x.JointLoop != null));
        Assert.True(loopSelfJumpsBlocks.Single().ChildJumps.All(x => x.JointMovement != null));
        Assert.False(loopSelfJumpsBlocks.Single().HasModulations);
        Assert.False(loopSelfJumpsBlocks.Single().IsModulation);
        Assert.Single(loopSelfJumpsBlocks.Single().NormalizationRootsFlow);
        Assert.True(loopSelfJumpsBlocks.Single().ChildJumps.All(x => x.Type == LoopSelfJumpType.SameKeyJoint));
    }

    [Fact]
    public void SelfJumpSameShift()
    {
        var (sequence, firstRoot) = InputDeltas(
            3, 3, 6,
            3, 3, 6,
            3, 9,
            3, 3, 6,
            3, 3, 6);

        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(3, loops.Count);
        Assert.Equal(loops[0].Normalized, loops[2].Normalized);
        Assert.Equal(loops[0].NormalizationRoot, loops[2].NormalizationRoot);
        Assert.Equal(loops[0].NormalizationShift, loops[2].NormalizationShift);
    }

    [Fact]
    public void SelfJumpDifferentShift()
    {
        var (sequence, firstRoot) = InputDeltas(
            3, 3, 6,
            3, 3, 6,
            6, 6,
            3, 3, 6,
            3, 3, 6);

        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(3, loops.Count);
        Assert.Equal(loops[0].Normalized, loops[2].Normalized);
        Assert.Equal(loops[0].NormalizationRoot, loops[2].NormalizationRoot);
        Assert.NotEqual(loops[0].NormalizationShift, loops[2].NormalizationShift);
    }

    [Fact]
    public void SelfJumpLong()
    {
        var (sequence, firstRoot) = InputDeltas(
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6,
            1, 2, 9,
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6);

        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(3, loops.Count);
        Assert.Equal(loops[0].Normalized, loops[2].Normalized);
        Assert.Equal(loops[0].NormalizationRoot, loops[2].NormalizationRoot);
        Assert.Equal(loops[0].NormalizationShift, loops[2].NormalizationShift);
    }

    [Theory]
    [InlineData(6, 2, 0, 1, "C C# D# E C C# D# E C C# D# E F# G D# E F# G D# E F# G")]
    [InlineData(6, 2, 0, 2, "C C# D# E F# C C# D# E F# C C# D# E F# G A D# E F# G A D# E F# G A")]
    [InlineData(6, 2, 0, 3, "C C# D# E F# G C C# D# E F# G C C# D# E F# G A Bb D# E F# G A Bb D# E F# G A Bb")]
    public void SelfJumpWithModulationWithMultipleOverlaps(byte root, int rootIndex1, int rootIndex2, int overlapLength, string sequenceChords)
    {
        var (sequence, firstRoot) = InputChords(sequenceChords);

        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Single(loopSelfJumpsBlocks.Single().ChildJumps);
        Assert.Equal(2, loopSelfJumpsBlocks.Single().NormalizationRootsFlow.Count);
        Assert.Equal(LoopSelfJumpType.ModulationOverlap, loopSelfJumpsBlocks.Single().ChildJumps.Single().Type);

        AssertNormalizedOverlaps(root, (rootIndex1, rootIndex2, overlapLength), loopSelfJumpsBlocks.Single().ChildJumps.Single());
    }

    [Fact]
    public void SelfJumpWithModulationWithOverlap()
    {
        var (sequence, firstRoot) = InputDeltas(1, 2, 1, 2, 6,
            1, 2, 1, 2, 6,
            1, 2,
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6);

        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(2, loops.Count);
        Assert.Equal(loops[0].Normalized, loops[1].Normalized);
        Assert.NotEqual(loops[0].NormalizationRoot, loops[1].NormalizationRoot);
        Assert.Null(loopSelfJumpsBlocks.Single().ChildJumps.Single().JointLoop);
        Assert.Null(loopSelfJumpsBlocks.Single().ChildJumps.Single().JointMovement);
        Assert.True(loopSelfJumpsBlocks.Single().HasModulations);
        Assert.True(loopSelfJumpsBlocks.Single().IsModulation);
        Assert.Equal(2, loopSelfJumpsBlocks.Single().NormalizationRootsFlow.Count);
        Assert.Equal(LoopSelfJumpType.ModulationOverlap, loopSelfJumpsBlocks.Single().ChildJumps.Single().Type);
    }

    [Fact]
    public void SelfJumpWithModulationWithJoint()
    {
        var (sequence, firstRoot) = InputDeltas(
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6,
            1, 10,
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6);

        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(3, loops.Count);
        Assert.Equal(loops[0].Normalized, loops[2].Normalized);
        Assert.NotEqual(loops[0].NormalizationRoot, loops[2].NormalizationRoot);
        Assert.NotNull(loopSelfJumpsBlocks.Single().ChildJumps.Single().JointLoop);
        Assert.NotNull(loopSelfJumpsBlocks.Single().ChildJumps.Single().JointMovement);
        Assert.True(loopSelfJumpsBlocks.Single().HasModulations);
        Assert.True(loopSelfJumpsBlocks.Single().IsModulation);
        Assert.Equal(2, loopSelfJumpsBlocks.Single().NormalizationRootsFlow.Count);
        Assert.Equal(LoopSelfJumpType.ModulationJointMovement, loopSelfJumpsBlocks.Single().ChildJumps.Single().Type);
    }

    [Fact]
    public void SelfJumpWithModulationWithMovement()
    {
        foreach (var chords in new[]
                 {
                     "C D E F C D E F C D E F F# G# A# B F# G# A# B F# G# A# B",
                     "C D E F C D E F C D E F G# A# B F# G# A# B F# G# A# B",
                     "C D E F C D E F C D E F A# B F# G# A# B F# G# A# B",
                     "C D E F C D E F C D E F B F# G# A# B F# G# A# B",
                 })
        {
            var (sequence, firstRoot) = InputChords(chords);

            var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
            var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
            TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

            Assert.Equal(2, loops.Count);
            Assert.Equal(loops[0].Normalized, loops[1].Normalized);
            Assert.NotEqual(loops[0].NormalizationRoot, loops[1].NormalizationRoot);
            Assert.True(loopSelfJumpsBlocks.Single().ChildJumps.Single()
                .SelectSingle(x => x.JointLoop == null && x.JointMovement != null));
            Assert.True(loopSelfJumpsBlocks.Single().HasModulations);
            Assert.True(loopSelfJumpsBlocks.Single().IsModulation);
            Assert.Equal(2, loopSelfJumpsBlocks.Single().NormalizationRootsFlow.Count);
            Assert.Equal(LoopSelfJumpType.ModulationJointMovement, loopSelfJumpsBlocks.Single().ChildJumps.Single().Type);
        }
    }

    [Fact]
    public void Random100X100WithChordTypes()
    {
        for (var i = 0; i < 100; i++)
        {
            ChordType GetRandomChordType() => (ChordType)Random.Shared.Next((int)ChordType.Major, (int)ChordType.Augmented + 1);
            var chordType = GetRandomChordType();
            var sequence = Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(0, 12)).Select(
                d => new CompactHarmonyMovement
                {
                    RootDelta = (byte)d,
                    FromType = chordType,
                    ToType = chordType = GetRandomChordType(),
                }).Where(m => m.FromType != m.ToType || m.RootDelta != 0).ToArray();

            var firstRoot = (byte)Random.Shared.Next(0, 12);
            var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
            var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
            TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks, false);
        }
    }

    [Theory]
    [InlineData(0, 8, "A# C F G Am C F G Am F G Am C F G Am C D")]
    [InlineData(0, 10, "A# C F G Am C F G Am G Am C F G Am C D")]
    [InlineData(0, 7, "Am Dm F E Am Dm F E Am E Am Dm F E Am Dm F E")]
    public void JointLoopIndicesCheck(int fromNormalizationRoot, int toNormalizationRoot, string inputChords)
    {
        var (sequence, firstRoot) = InputChords(inputChords);
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        var loopSelfMultiJumpBlock = loopSelfJumpsBlocks.Single();
        var loopSelfJumpBlock = loopSelfMultiJumpBlock.ChildJumps.Single();

        Assert.Equal(
            (fromNormalizationRoot, toNormalizationRoot),
            ToNormalizedRoots(loopSelfJumpBlock.JumpPoints!.Value, loopSelfJumpBlock.Normalized, loopSelfJumpBlock.Loop1.NormalizationRoot));
    }

    [Fact]
    public void MassiveOverlapsCheck()
    {
        var (sequence, firstRoot) = InputChords("A F A B C D A Bm B C Cm D Dm A F D Dm A F");
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfJumpsBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        var massiveOverlaps = indexExtractor.FindMassiveOverlapsBlocks(loops);

        Assert.Single(massiveOverlaps);

        Assert.Equal(2, massiveOverlaps.Single().InternalLoops.Count);
    }

    [Fact]
    public void MultipleJumpsBlocks()
    {
        var (sequence, firstRoot) = InputChords("Am C D Am C D Am D Am C D E C D E C E C D E C D E");
        var loops = indexExtractor.FindSimpleLoops(sequence, indexExtractor.CreateRoots(sequence, firstRoot));
        var loopSelfMultiJumpBlocks = indexExtractor.FindSelfJumps(sequence, loops);
        Assert.Equal(2, loopSelfMultiJumpBlocks.Count);
    }

    private void TraceAndTest(
        ReadOnlyMemory<CompactHarmonyMovement> sequence,
        byte firstRoot,
        List<LoopBlock> loops,
        List<LoopSelfMultiJumpBlock> loopSelfJumpsBlocks,
        bool trace = true,
        bool visualize = true)
    {
        var massiveOverlaps = indexExtractor.FindMassiveOverlapsBlocks(loops);
        var anyMassiveOverlaps = massiveOverlaps.Any();

        Assert.All(massiveOverlaps, o => Assert.DoesNotContain(o.Edge1, o.InternalLoops));
        Assert.All(massiveOverlaps, o => Assert.DoesNotContain(o.Edge2, o.InternalLoops));

        AssertLoopsSequencePossible(loops, anyMassiveOverlaps);
        AssertLoopsSequencePossible(loopSelfJumpsBlocks, false);

        var roots = indexExtractor.CreateRoots(sequence, firstRoot);

        TraceAndTest(indexExtractor.FindBlocks(sequence, roots, BlocksExtractionLogic.Loops), anyMassiveOverlaps);
        TraceAndTest(indexExtractor.FindBlocks(sequence, roots, BlocksExtractionLogic.ReplaceWithSelfJumps), anyMassiveOverlaps);
        TraceAndTest(indexExtractor.FindBlocks(sequence, roots, BlocksExtractionLogic.ReplaceWithSelfMultiJumps), anyMassiveOverlaps);

        // sequence integrity test
        Assert.True(MemoryMarshal.ToEnumerable(sequence).AsPairs().All(p => p.current.FromType == p.previous.ToType), 
            "sequential to type and from type mismatch");
        Assert.All(MemoryMarshal.ToEnumerable(sequence), m => Assert.False(m.RootDelta == 0 && m.FromType == m.ToType,
            "null movements in the sequence"));

        Assert.False(loops.Select(l => l.StartIndex).AnyDuplicates());
        Assert.False(loops.Select(l => l.EndIndex).AnyDuplicates());
        Assert.False(loopSelfJumpsBlocks.Select(l => l.StartIndex).AnyDuplicates());
        Assert.False(loopSelfJumpsBlocks.Select(l => l.EndIndex).AnyDuplicates());

        Assert.All(loops, l => Assert.True(l.LoopLength > 1));

        Assert.All(loops, l => Assert.Equal(
            l.Successions == Math.Round(l.Successions),
            l.Occurrences == Math.Round(l.Occurrences)));

        Assert.All(loops, l => Assert.True(l.Occurrences >= 1));
        Assert.All(loops, l => Assert.True(l.Successions >= 0));

        Assert.All(loops, l => Assert.False(
            l.Occurrences == Math.Round(l.Occurrences)
            && l.EachChordCoveredTimes == Math.Round(l.EachChordCoveredTimes)));

        Assert.All(loops, l => Assert.True(l.EachChordCoveredTimes > 1 + float.Epsilon));

        Assert.All(loops, l => Assert.Equal(
            l.EachChordCoveredTimes == Math.Round(l.EachChordCoveredTimes),
            roots[l.StartIndex + l.LoopLength - 1] == roots[l.EndIndex + 1] && sequence.Span[l.StartIndex + l.LoopLength - 1].FromType == sequence.Span[l.EndIndex].ToType));

        Assert.All(loops, l => Assert.Equal(
            l.EachChordCoveredTimes == Math.Round(l.EachChordCoveredTimes),
            l.EachChordCoveredTimesWhole));

        Assert.All(loops, l => Assert.Equal(
            l.Successions == Math.Round(l.Successions),
            roots[l.StartIndex] == roots[l.EndIndex + 1] && sequence.Span[l.StartIndex].FromType == sequence.Span[l.EndIndex].ToType));

        Assert.All(loops, l => Assert.Equal(
            l.Successions == Math.Round(l.Successions),
            l.SuccessionsWhole));

        Assert.All(loops, l => Assert.Equal(
            l.Successions >= 1,
            l.SuccessionsSignificant));

        Assert.All(loops, l => Assert.Equal(
            l.EachChordCoveredTimes >= 2,
            l.EachChordCoveredTimesSignificant));

        Assert.All(loops, l => Assert.Equal(
            l.Successions,
            l.Occurrences - 1));

        string CreateRootsTrace(IBlock block) => progressionsVisualizer.CreateRootsTraceByIndices(sequence, roots, block.StartIndex, block.EndIndex, out _);

        if (trace)
        {
            logger.LogInformation(
                $"loops: {loops.Count}, self jumps: {loopSelfJumpsBlocks.Count}, roots: {(visualize ? null : progressionsVisualizer.CreateRootsTraceByIndices(sequence, roots, 0, sequence.Length - 1, out _))}");

            if (visualize)
            {
                var all = indexExtractor.FindBlocks(sequence, roots, BlocksExtractionLogic.All);
                var result = progressionsVisualizer.VisualizeBlocksAsOne(sequence, roots, all, false);
                logger.LogInformation(Environment.NewLine + result + Environment.NewLine);
            }
        }

        // blocks sequential loops test
        Assert.True(loopSelfJumpsBlocks.All(x => x.ChildJumps.AsPairs().All(p => p.current.Loop1 == p.previous.Loop2)));

        foreach (var loopSelfJumpsBlock in loopSelfJumpsBlocks)
        {
            // has modulations integrity
            Assert.Equal(loopSelfJumpsBlock.HasModulations, loopSelfJumpsBlock.ChildJumps.Any(x => x.IsModulation));
            Assert.True(!loopSelfJumpsBlock.IsModulation || loopSelfJumpsBlock.HasModulations); // IsModulation => HasModulation

            // normalization roots flow
            Assert.True(loopSelfJumpsBlock.NormalizationRootsFlow.AsPairs().All(p => p.current != p.previous));
            Assert.Equal(loopSelfJumpsBlock.HasModulations, loopSelfJumpsBlock.NormalizationRootsFlow.Count > 1);
            Assert.Equal(loopSelfJumpsBlock.IsModulation, loopSelfJumpsBlock.NormalizationRootsFlow[0] != loopSelfJumpsBlock.NormalizationRootsFlow[^1]);

            // child self jumps are linked
            Assert.True(loopSelfJumpsBlock.ChildJumps.AsPairs().All(x => x.current.Loop1 == x.previous.Loop2));

            // child loops are the same as child jumps
            Assert.Equal(
                loopSelfJumpsBlock.ChildLoops, 
                loopSelfJumpsBlock.ChildJumps
                    .SelectMany((x, i) => i == 0 ? (LoopBlock?[])[x.Loop1, x.JointLoop, x.Loop2] : [x.JointLoop, x.Loop2])
                    .Where(x => x != null));

            var endMovement = loopSelfJumpsBlock.StartIndex + loopSelfJumpsBlock.LoopLength - 1;

            var normalized = loopSelfJumpsBlock.Normalized.DeserializeLoop();

            var childJumps = string.Join(Environment.NewLine, loopSelfJumpsBlock.ChildJumps.Select(j =>
                {
                    Assert.True(j.Loop2.StartIndex - j.Loop1.EndIndex is 1 or 2 or <= 0);

                    string? jumpPoints = null;
                    if (j.JumpPoints.HasValue)
                    {
                        var normalizedRoots1 = indexExtractor.CreateRoots(normalized, j.Loop1.NormalizationRoot);
                        var normalizedRoots2 = indexExtractor.CreateRoots(normalized, j.Loop2.NormalizationRoot);
                        jumpPoints = $"[{j.JumpPoints}] {normalizedRoots1[j.JumpPoints.Value.root1Index]} -> {normalizedRoots2[j.JumpPoints.Value.root2Index]}";

                        switch (j.Type)
                        {
                            case LoopSelfJumpType.ModulationJointMovement:
                                break;
                            case LoopSelfJumpType.SameKeyJoint:
                                Assert.Equal(j.Loop1.NormalizationRoot, j.Loop2.NormalizationRoot);
                                break;
                            case LoopSelfJumpType.ModulationAmbiguousChord:
                                Assert.Equal(normalizedRoots1[j.JumpPoints.Value.root1Index], normalizedRoots2[j.JumpPoints.Value.root2Index]);
                                break;
                        }
                    }

                    if (j.OverlapJumpPoints.HasValue)
                    {
                        var normalizedRoots1 = indexExtractor.CreateRoots(normalized, j.Loop1.NormalizationRoot);
                        var normalizedRoots2 = indexExtractor.CreateRoots(normalized, j.Loop2.NormalizationRoot);
                        Assert.Equal(normalizedRoots1[j.OverlapJumpPoints.Value.overlapStartRoot1Index], normalizedRoots2[j.OverlapJumpPoints.Value.overlapStartRoot2Index]);
                        jumpPoints = $"[{j.OverlapJumpPoints}] {normalizedRoots1[j.OverlapJumpPoints.Value.overlapStartRoot1Index]} -> {normalizedRoots2[j.OverlapJumpPoints.Value.overlapStartRoot2Index]}";
                    }

                    var endMovement = j.StartIndex + j.LoopLength - 1;
                    return $"SJ {j.LoopLength}" +
                           $" + {j.EndIndex - endMovement}" +
                           $" ({j.StartIndex}..{endMovement}..{j.EndIndex}) {j.Type}:" +
                           $" (jump points = {jumpPoints})" +
                           $" (keys = {j.Loop1.NormalizationRoot} => {j.Loop2.NormalizationRoot}, c = {j.ModulationDelta})" +
                           $" (is-modulation={j.IsModulation})" +
                           $" (joint-loop={j.JointLoop != null})" +
                           $" (joint-movement={j.JointMovement != null})" +
                           $" roots: {CreateRootsTrace(j)}";
                }));

            if (trace)
            {
                logger.LogInformation($"SJS {loopSelfJumpsBlock.LoopLength}" +
                                      $" + {loopSelfJumpsBlock.EndIndex - endMovement}" +
                                      $" ({loopSelfJumpsBlock.StartIndex}..{endMovement}..{loopSelfJumpsBlock.EndIndex}):" +
                                      $" (keys={string.Join(", ", loopSelfJumpsBlock.NormalizationRootsFlow)})" +
                                      $" (has-modulations={loopSelfJumpsBlock.HasModulations})" +
                                      $"\nall roots: {CreateRootsTrace(loopSelfJumpsBlock)};" +
                                      $"\njumps:\n{childJumps}");
            }
        }

        foreach (var loopSelfJumpBlock in loopSelfJumpsBlocks.SelectMany(x => x.ChildJumps))
        {
            // key change
            Assert.Equal(!loopSelfJumpBlock.IsModulation, loopSelfJumpBlock.ModulationDelta == 0);
            Assert.InRange(loopSelfJumpBlock.ModulationDelta, 0, 11);

            // from and to are not equal and not sequential for non-modulated jumps
            if (loopSelfJumpBlock.Type is LoopSelfJumpType.SameKeyJoint)
            {
                Assert.Null(loopSelfJumpBlock.OverlapJumpPoints);
                Assert.NotNull(loopSelfJumpBlock.JumpPoints);
                var from = loopSelfJumpBlock.JumpPoints.Value.root1Index;
                var to = loopSelfJumpBlock.JumpPoints.Value.root2Index;
                Assert.NotEqual(from, to);
                Assert.NotEqual((from + 1) % loopSelfJumpBlock.LoopLength, to);
            }
            else if (loopSelfJumpBlock.Type == LoopSelfJumpType.ModulationOverlap)
            {
                Assert.NotNull(loopSelfJumpBlock.OverlapJumpPoints);
                Assert.Null(loopSelfJumpBlock.JumpPoints);
            }
            else
            {
                Assert.Null(loopSelfJumpBlock.OverlapJumpPoints);
            }

            // the joint movement null reason and the loops gap need to correspond
            var loopsGap = loopSelfJumpBlock.Loop2.StartIndex - loopSelfJumpBlock.Loop1.EndIndex;
            if (loopSelfJumpBlock.JointMovement == null)
            {
                switch (loopsGap, loopSelfJumpBlock.Type)
                {
                    case (1, LoopSelfJumpType.ModulationAmbiguousChord):
                        break;
                    case ( < 1, LoopSelfJumpType.ModulationOverlap):
                        break;
                    default:
                        Assert.Fail("The joint movement null reason and the loops gap do not correspond.");
                        break;
                }
            }

            // end and start with the same loop and the jumps block normalized is the same
            Assert.Equal(loopSelfJumpBlock.Loop1.Normalized, loopSelfJumpBlock.Loop2.Normalized);
            Assert.Equal(loopSelfJumpBlock.Loop1.Normalized, loopSelfJumpBlock.Normalized);

            // the middle loop is not the same
            Assert.NotEqual(loopSelfJumpBlock.JointLoop?.Normalized, loopSelfJumpBlock.Normalized);

            // the middle loop is inside the outer loops
            if (loopSelfJumpBlock.JointLoop != null)
            {
                Assert.True(loopSelfJumpBlock.JointLoop.StartIndex > loopSelfJumpBlock.Loop1.StartIndex);
                Assert.True(loopSelfJumpBlock.JointLoop.EndIndex < loopSelfJumpBlock.Loop2.EndIndex);
            }

            switch (loopSelfJumpBlock.Type)
            {
                case LoopSelfJumpType.SameKeyJoint:
                    // exactly one extra movement
                    Assert.Equal(loopSelfJumpBlock.Loop1.EndIndex + 2, loopSelfJumpBlock.Loop2.StartIndex);
                    Assert.NotNull(loopSelfJumpBlock.JointLoop);
                    Assert.NotNull(loopSelfJumpBlock.JointMovement);
                    Assert.False(loopSelfJumpBlock.IsModulation);
                    break;

                case LoopSelfJumpType.ModulationJointMovement:
                    // exactly one extra movement
                    Assert.Equal(loopSelfJumpBlock.Loop1.EndIndex + 2, loopSelfJumpBlock.Loop2.StartIndex);
                    Assert.NotNull(loopSelfJumpBlock.JointMovement);
                    Assert.True(loopSelfJumpBlock.IsModulation);
                    break;
                case LoopSelfJumpType.ModulationAmbiguousChord:
                    // exactly zero extra movements
                    Assert.Equal(loopSelfJumpBlock.Loop1.EndIndex + 1, loopSelfJumpBlock.Loop2.StartIndex);
                    Assert.Null(loopSelfJumpBlock.JointMovement);
                    Assert.True(loopSelfJumpBlock.IsModulation);
                    break;
                case LoopSelfJumpType.ModulationOverlap:
                    Assert.True(loopSelfJumpBlock.Loop1.EndIndex + 1 > loopSelfJumpBlock.Loop2.StartIndex);
                    Assert.Equal(loopSelfJumpBlock.OverlapJumpPoints!.Value.overlapLength, loopSelfJumpBlock.Loop1.EndIndex + 1 - loopSelfJumpBlock.Loop2.StartIndex);
                    Assert.Null(loopSelfJumpBlock.JointMovement);
                    Assert.True(loopSelfJumpBlock.IsModulation);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        foreach (var loop in loops)
        {
            var endLoopIndex = loop.StartIndex + loop.LoopLength - 1;

            Assert.Equal(loop.LoopLength, endLoopIndex - loop.StartIndex + 1);
            Assert.True(endLoopIndex <= loop.EndIndex);
            Assert.InRange(loop.StartIndex, 0, sequence.Length - 1);
            Assert.InRange(endLoopIndex, 0, sequence.Length - 1);
            Assert.InRange(loop.EndIndex, 0, sequence.Length - 1);
            
            // loop normalization shift correctly set value test
            Assert.Equal((loop.Normalized, loop.NormalizationShift), (indexExtractor.GetNormalizedProgression(loop.Loop, out var shift, out _).SerializeLoop(), shift));

            var rootsTrace = CreateRootsTrace(loop);

            if (trace)
            {
                logger.LogInformation($"L {loop.LoopLength}" +
                                      $" + {loop.EndIndex - endLoopIndex}" +
                                      $" ({loop.StartIndex}..{endLoopIndex}..{loop.EndIndex}):" +
                                      $" {rootsTrace};");
            }

            var normalized = loop.Normalized.DeserializeLoop();
            var normalizedRoots = indexExtractor.CreateRoots(normalized, loop.NormalizationRoot);
            var normalizedRootsIndices = Enumerable.Range(0, loop.EndIndex - loop.StartIndex + 1)
                .Prepend(-1 + normalized.Length)
                .Select(x => x + loop.NormalizationShift)
                .Select(i => (i % normalized.Length) + 1)
                .ToList();
            var normalizedRecreation = string.Join(" ", normalizedRootsIndices
                .Select(i => $"{new Note(normalizedRoots[i]).Representation(new())}{(i == 0 ? normalized.Span[0].FromType : normalized.Span[i - 1].ToType).ChordTypeToString()}"));

            // loop normalized recreation test and the normalization start root test
            Assert.Equal(rootsTrace, normalizedRecreation);
            Assert.Equal(normalizedRoots[0], loop.NormalizationRoot);

            // loop type cycle test
            Assert.Equal(loop.Loop.Span[0].FromType, loop.Loop.Span[^1].ToType);

            // loop lower boundary test
            var rootIndexBeforeLoop = loop.StartIndex - 1;
            if (rootIndexBeforeLoop >= 0)
            {
                Assert.NotEqual(
                    (roots[rootIndexBeforeLoop + loop.LoopLength], sequence.Span[endLoopIndex].FromType),
                    (roots[rootIndexBeforeLoop], sequence.Span[loop.StartIndex - 1].FromType));
            }
            
            // loop upper boundary test
            if (loop.EndIndex + 1 < sequence.Length)
            {
                Assert.NotEqual(
                    (roots[loop.EndIndex - loop.LoopLength + 2], sequence.Span[loop.EndIndex - loop.LoopLength + 1].ToType),
                    (roots[loop.EndIndex + 2], sequence.Span[loop.EndIndex + 1].ToType));
            }
        }
    }

    private void TraceAndTest(List<IBlock> blocks, bool anyMassiveOverlaps)
    {
        AssertLoopsSequencePossible(blocks, anyMassiveOverlaps);

        // sequences do not overlap with anything else
        Assert.False(blocks.OfType<SequenceBlock>().Cast<IBlock>().Any(s => blocks.Any(l => l != s && (
            l.StartIndex >= s.StartIndex && l.StartIndex <= s.EndIndex // l.StartIndex within the sequence
            || l.EndIndex >= s.StartIndex && l.EndIndex <= s.EndIndex)))); // l.EndIndex within the sequence
    }

    private void AssertLoopsSequencePossible(IReadOnlyList<IBlock> blocks, bool anyMassiveOverlaps)
    {
        // there's no such block that contains another block
        Assert.False(blocks.Any(x => blocks.Where(y => y != x)
            .Any(y => x.StartIndex <= y.StartIndex && x.EndIndex >= y.EndIndex)));

        // any movement is covered by a maximum of 2 multijumps
        Assert.False(blocks
            .SelectMany(x => Enumerable.Range(x.StartIndex, x.EndIndex - x.StartIndex + 1).Select(i => (i, isSimple: x is LoopBlock)))
            .GroupBy(x => x.i)
            .Any(x => anyMassiveOverlaps

                // if multiple simple blocks, treat them as one
                ? x.Count()
                  - x.Count(x => x.isSimple)
                  + (x.Any(x => x.isSimple) ? 1 : 0)
                  > 2

                // no massive overlaps, not more than two are allowed, even simple
                : x.Count() > 2));

        if (anyMassiveOverlaps)
        {
            Assert.True(blocks
                .SelectMany(x => Enumerable.Range(x.StartIndex, x.EndIndex - x.StartIndex + 1).Select(i => (i, isSimple: x is LoopBlock)))
                .GroupBy(x => x.i)
                .Any(x => x.Count() > 2));
        }
    }

    private void AssertNormalized((byte root1, byte root2) roots, (int rootIndex1, int rootIndex2) rootIndices, LoopSelfJumpBlock loopSelfJumpBlock)
    {
        Assert.Equal(roots, ToNormalizedRoots(
            loopSelfJumpBlock.JumpPoints!.Value,
            loopSelfJumpBlock.Normalized,
            loopSelfJumpBlock.Loop1.NormalizationRoot,
            loopSelfJumpBlock.Loop2.NormalizationRoot));
        Assert.Equal(rootIndices, loopSelfJumpBlock.JumpPoints);
    }

    private void AssertNormalizedOverlaps(byte root, (int rootIndex1, int rootIndex2, int overlapLength) rootIndices, LoopSelfJumpBlock loopSelfJumpBlock)
    {
        Assert.Equal((root, root), ToNormalizedRoots(
            (loopSelfJumpBlock.OverlapJumpPoints!.Value.overlapStartRoot1Index, loopSelfJumpBlock.OverlapJumpPoints!.Value.overlapStartRoot2Index),
            loopSelfJumpBlock.Normalized,
            loopSelfJumpBlock.Loop1.NormalizationRoot,
            loopSelfJumpBlock.Loop2.NormalizationRoot));
        Assert.Equal(rootIndices, loopSelfJumpBlock.OverlapJumpPoints);
    }

    private (ReadOnlyMemory<CompactHarmonyMovement> sequence, byte firstRoot) InputDeltas(params int[] deltas)
        => InputDeltas(deltas, 0);

    private (ReadOnlyMemory<CompactHarmonyMovement> sequence, byte firstRoot) InputDeltas(params byte[] deltas)
        => InputDeltas(deltas, null);

    private (ReadOnlyMemory<CompactHarmonyMovement> sequence, byte firstRoot) InputDeltas(IEnumerable<int> deltas, byte? constRoot = null)
        => InputDeltas(deltas.Select(x => (byte)x), constRoot);

    private (ReadOnlyMemory<CompactHarmonyMovement> sequence, byte firstRoot) InputDeltas(IEnumerable<byte> deltas, byte? constRoot = null)
    {
        var firstRoot = constRoot ?? (byte)Random.Shared.Next(1, 12);
        var sequence = deltas.Select(d => new CompactHarmonyMovement
        {
            RootDelta = (byte)d,
            ToType = ChordType.Augmented,
            FromType = ChordType.Augmented,
        }).ToArray();

        return (sequence, firstRoot);
    }

    private (ReadOnlyMemory<CompactHarmonyMovement> sequence, byte firstRoot) InputChords(string chords)
        => progressionsBuilder.BuildProgression(chords
                .Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x =>
                {
                    var note = Note.CharactersToNotes[x[0]];
                    x = x.Substring(1);
                    if (x.Length > 0 && x[0] is '#' or 'b')
                    {
                        note = x[0] switch
                        {
                            '#' => note.Sharp(),
                            'b' => note.Flat(),
                            _ => throw new ArgumentOutOfRangeException(),
                        };

                        x = x.Substring(1);
                    }

                    return chordDataParser.GetProgressionData(
                        $"[!{note.Value}!]{x}");
                })
                .ToList())
            .SelectSingle(p => (p.ExtendedHarmonyMovementsSequences.Single().Movements.Select(x => x.Compact()).ToArray(), p.ExtendedHarmonyMovementsSequences.Single().FirstRoot));
}