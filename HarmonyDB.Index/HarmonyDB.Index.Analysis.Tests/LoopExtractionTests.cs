using System.Runtime.InteropServices;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.Analysis.Tests.Attempt;
using HarmonyDB.Index.Analysis.Tests.Attempt.Models;
using HarmonyDB.Index.Analysis.Tools;
using Microsoft.Extensions.Logging;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Tests;

public class LoopExtractionTests(ILogger<LoopExtractionTests> logger, ChordDataParser chordDataParser, ProgressionsBuilder progressionsBuilder, Service service)
{
    [Fact]
    public void NoneShort()
    {
        var (sequence, firstRoot) = InputDeltas(1);
        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);

        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Empty(loops);
    }

    [Fact]
    public void NoneLong()
    {
        var (sequence, firstRoot) = InputDeltas(Enumerable.Repeat(7, 11));
        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
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
        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
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
        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Single(loops);
        Assert.Equal((0, 12 * 8 - 1), loops[0].SelectSingle(x => (x.StartIndex, x.EndIndex)));
    }

    [Fact]
    public void OneLong()
    {
        var (sequence, firstRoot) = InputDeltas(Enumerable.Repeat(7, 12), 0);
        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Single(loops);
        Assert.Equal((0, 11), loops[0].SelectSingle(x => (x.StartIndex, x.EndIndex)));
    }

    [Fact]
    public void EndingPlusOne()
    {
        var (sequence, firstRoot) = InputDeltas(Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12)).Concat([5, 7, 5, 7, 1]));
        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(103, loops[^1].EndIndex);
        Assert.Equal(2, loops[^1].SelectSingle(x => x.LoopLength));
    }

    [Fact]
    public void Ending()
    {
        var (sequence, firstRoot) = InputDeltas(Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12)).Concat([5, 7, 5, 7]));
        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(103, loops[^1].EndIndex);
        Assert.Equal(2, loops[^1].SelectSingle(x => x.LoopLength));
    }

    [Fact]
    public void Beginning()
    {
        var (sequence, firstRoot) = InputDeltas(new[] { 5, 7, 5, 7, 5 }.Concat(Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12))));
        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
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

        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(1, loops[0].StartIndex);
        Assert.Equal(2, loops[0].SelectSingle(x => x.LoopLength));
        Assert.True(loops[0].EndIndex >= 5);
    }

    [Fact]
    public void Random100X100()
    {
        HashSet<int> modulationOverlapsAccumulator = new();
        for (var i = 0; i < 100; i++)
        {
            var (sequence, firstRoot) = InputDeltas(Enumerable.Range(0, 100).Select(_ => Random.Shared.Next(1, 12)));
            var loops = service.FindSimpleLoops(sequence, firstRoot);
            var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
            TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks, modulationOverlapsAccumulator, false);
        }

        logger.LogInformation("modulationOverlapsAccumulator: {data}", string.Join(", ", modulationOverlapsAccumulator.OrderBy(x => x)));
    }

    [Fact]
    public void ChordsInputWithAlterations()
    {
        var sequenceChords = "C# F# C# F# C# G# C# G# C# G# C# G# F# C# F# C# F# C# F#";
        var (sequence, firstRoot) = InputChords(sequenceChords);
        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
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

        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
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

        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
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

        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
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
        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
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
        => Loop.Deserialize(normalized)
            .SelectSingle(s => (s, roots: service.CreateRoots(s, normalizationRoot1)))
            .SelectSingle(r =>
                normalizationRoot2 == null
                    ? (roots1: r.roots, roots2: r.roots)
                    : (roots1: r.roots, roots2: service.CreateRoots(r.s, normalizationRoot2.Value)))
            .SelectSingle(r => (r.roots1[rootIndices.rootIndex1], r.roots2[rootIndices.rootIndex2]));

    [Fact]
    public void SelfJumpMultiModulationOneJointOneAmbiguous()
    {
        //                    1 4 1 4 1               4 1 4 1 4 1 4
        //                            4 1 4 1 4 1 4 1
        var sequenceChords = "C F C F C G C G C G C G F C F C F C F";
        var (sequence, firstRoot) = InputChords(sequenceChords);
        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
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
        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
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
        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
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
        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
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

        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
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

        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
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

        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        Assert.Equal(3, loops.Count);
        Assert.Equal(loops[0].Normalized, loops[2].Normalized);
        Assert.Equal(loops[0].NormalizationRoot, loops[2].NormalizationRoot);
        Assert.Equal(loops[0].NormalizationShift, loops[2].NormalizationShift);
    }

    [Fact]
    public void SelfJumpWithModulationWithMultipleOverlaps()
    {
        var overlaps = new List<int>();
        foreach (var sequenceChords in new[]
                 {
                     "C C# D# E F# C C# D# E F# C C# D# E F# G A D# E F# G A D# E F# G A",
                     "C C# D# E F# G C C# D# E F# G C C# D# E F# G A Bb D# E F# G A Bb D# E F# G A Bb",
                     "C C# D# E C C# D# E C C# D# E F# G D# E F# G D# E F# G",
                 })
        {
            var (sequence, firstRoot) = InputChords(sequenceChords);

            var loops = service.FindSimpleLoops(sequence, firstRoot);
            var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
            HashSet<int> modulationOverlapsAccumulator = new();
            TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks, modulationOverlapsAccumulator);
            logger.LogInformation("modulationOverlapsAccumulator: {data}",
                string.Join(", ", modulationOverlapsAccumulator.OrderBy(x => x)));

            Assert.Single(loopSelfJumpsBlocks.Single().ChildJumps);
            Assert.Equal(2, loopSelfJumpsBlocks.Single().NormalizationRootsFlow.Count);
            Assert.Equal(LoopSelfJumpType.ModulationOverlap, loopSelfJumpsBlocks.Single().ChildJumps.Single().Type);
            Assert.Single(modulationOverlapsAccumulator);
            overlaps.Add(modulationOverlapsAccumulator.Single());
        }

        Assert.Equal(2, overlaps[0]);
        Assert.Equal(3, overlaps[1]);
        Assert.Equal(1, overlaps[2]);
    }

    [Fact]
    public void SelfJumpWithModulationWithOverlap()
    {
        var (sequence, firstRoot) = InputDeltas(1, 2, 1, 2, 6,
            1, 2, 1, 2, 6,
            1, 2,
            1, 2, 1, 2, 6,
            1, 2, 1, 2, 6);

        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
        HashSet<int> modulationOverlapsAccumulator = new();
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks, modulationOverlapsAccumulator);
        logger.LogInformation("modulationOverlapsAccumulator: {data}", string.Join(", ", modulationOverlapsAccumulator.OrderBy(x => x)));

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

        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
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

            var loops = service.FindSimpleLoops(sequence, firstRoot);
            var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
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
        HashSet<int> modulationOverlapsAccumulator = new();
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
                }).ToArray();

            var firstRoot = (byte)Random.Shared.Next(0, 12);
            var loops = service.FindSimpleLoops(sequence, firstRoot);
            var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
            TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks, modulationOverlapsAccumulator, false);
        }

        logger.LogInformation("modulationOverlapsAccumulator: {data}", string.Join(", ", modulationOverlapsAccumulator.OrderBy(x => x)));
    }

    [Theory]
    [InlineData(0, 8, "A# C F G Am C F G Am F G Am C F G Am C D")]
    [InlineData(0, 10, "A# C F G Am C F G Am G Am C F G Am C D")]
    [InlineData(0, 7, "Am Dm F E Am Dm F E Am E Am Dm F E Am Dm F E")]
    public void JointLoopIndicesCheck(int fromNormalizationRoot, int toNormalizationRoot, string inputChords)
    {
        var (sequence, firstRoot) = InputChords(inputChords);
        var loops = service.FindSimpleLoops(sequence, firstRoot);
        var loopSelfJumpsBlocks = service.FindSelfJumps(sequence, loops);
        TraceAndTest(sequence, firstRoot, loops, loopSelfJumpsBlocks);

        var loopSelfMultiJumpBlock = loopSelfJumpsBlocks.Single();
        var loopSelfJumpBlock = loopSelfMultiJumpBlock.ChildJumps.Single();

        Assert.Equal(
            (fromNormalizationRoot, toNormalizationRoot),
            ToNormalizedRoots(loopSelfJumpBlock.JumpPoints!.Value, loopSelfJumpBlock.Normalized, loopSelfJumpBlock.Loop1.NormalizationRoot));
    }
    
    private void TraceAndTest(
        ReadOnlyMemory<CompactHarmonyMovement> sequence,
        byte firstRoot,
        List<LoopBlock> loops,
        List<LoopSelfMultiJumpBlock> loopSelfJumpsBlocks,
        HashSet<int>? modulationOverlapsAccumulator = null,
        bool trace = true)
    {
        // sequence integrity test
        Assert.True(MemoryMarshal.ToEnumerable(sequence).WithPrevious().Skip(1).All(p => p.current.FromType == p.previous!.Value.ToType), "sequential to type and from type mismatch");

        var roots = service.CreateRoots(sequence, firstRoot);

        string CreateRootsTrace(IBlock block) => CreateRootsTraceByIndices(block.StartIndex, block.EndIndex);

        string CreateRootsTraceByIndices(int coveredByStartIndex, int coveredByEndIndex) =>
            string.Join(" ", Enumerable.Range(coveredByStartIndex, coveredByEndIndex - coveredByStartIndex + 1)
                .Select(i => i + 1)
                .Prepend(coveredByStartIndex)
                .Select(i =>
                    $"{roots[i]}{(i == 0 ? sequence.Span[0].FromType : sequence.Span[i - 1].ToType).ChordTypeToString()}"));

        if (trace)
        {
            logger.LogInformation(
                $"loops: {loops.Count}, self jumps: {loopSelfJumpsBlocks.Count}, roots: {CreateRootsTraceByIndices(0, sequence.Length - 1)}");
        }

        // blocks sequential loops test
        Assert.True(loopSelfJumpsBlocks.All(x => x.ChildJumps.WithPrevious().Skip(1).All(p => p.current.Loop1 == p.previous!.Loop2)));

        foreach (var loopSelfJumpsBlock in loopSelfJumpsBlocks)
        {
            // has modulations integrity
            Assert.Equal(loopSelfJumpsBlock.HasModulations, loopSelfJumpsBlock.ChildJumps.Any(x => x.IsModulation));
            Assert.True(!loopSelfJumpsBlock.IsModulation || loopSelfJumpsBlock.HasModulations); // IsModulation => HasModulation

            // normalization roots flow
            Assert.True(loopSelfJumpsBlock.NormalizationRootsFlow.WithPrevious().Skip(1).All(p => p.current != p.previous));
            Assert.Equal(loopSelfJumpsBlock.HasModulations, loopSelfJumpsBlock.NormalizationRootsFlow.Count > 1);
            Assert.Equal(loopSelfJumpsBlock.IsModulation, loopSelfJumpsBlock.NormalizationRootsFlow[0] != loopSelfJumpsBlock.NormalizationRootsFlow[^1]);

            var endMovement = loopSelfJumpsBlock.StartIndex + loopSelfJumpsBlock.LoopLength - 1;

            var normalized = Loop.Deserialize(loopSelfJumpsBlock.Normalized);

            var childJumps = string.Join(Environment.NewLine, loopSelfJumpsBlock.ChildJumps.Select(j =>
                {
                    Assert.True(j.Loop2.StartIndex - j.Loop1.EndIndex is 1 or 2 or <= 0);

                    string? jumpPoints = null;
                    var points = j.JumpPoints;
                    if (points.HasValue)
                    {
                        var normalizedRoots1 = service.CreateRoots(normalized, j.Loop1.NormalizationRoot);
                        var normalizedRoots2 = service.CreateRoots(normalized, j.Loop2.NormalizationRoot);
                        jumpPoints = $"[{points}] {normalizedRoots1[points.Value.rootIndex1]} -> {normalizedRoots2[points.Value.rootIndex2]}";

                        switch (j.Type)
                        {
                            case LoopSelfJumpType.ModulationJointMovement:
                                break;
                            case LoopSelfJumpType.SameKeyJoint:
                                Assert.Equal(j.Loop1.NormalizationRoot, j.Loop2.NormalizationRoot);
                                break;
                            case LoopSelfJumpType.ModulationAmbiguousChord:
                                Assert.Equal(normalizedRoots1[points.Value.rootIndex1], normalizedRoots2[points.Value.rootIndex2]);
                                break;
                        }
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
                Assert.NotNull(loopSelfJumpBlock.JumpPoints);
                var from = loopSelfJumpBlock.JumpPoints.Value.rootIndex1;
                var to = loopSelfJumpBlock.JumpPoints.Value.rootIndex2;
                Assert.NotEqual(from, to);
                Assert.NotEqual((from + 1) % loopSelfJumpBlock.LoopLength, to);
            }
            else if (loopSelfJumpBlock.Type == LoopSelfJumpType.ModulationOverlap)
            {
                Assert.Null(loopSelfJumpBlock.JumpPoints);
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
                    modulationOverlapsAccumulator?.Add(loopSelfJumpBlock.Loop1.EndIndex + 1 - loopSelfJumpBlock.Loop2.StartIndex); // from 1 to infinity
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
            Assert.Equal((loop.Normalized, loop.NormalizationShift), (Loop.Serialize(Loop.GetNormalizedProgression(loop.Loop, out var shift, out _)), shift));

            var rootsTrace = CreateRootsTrace(loop);

            if (trace)
            {
                logger.LogInformation($"L {loop.LoopLength}" +
                                      $" + {loop.EndIndex - endLoopIndex}" +
                                      $" ({loop.StartIndex}..{endLoopIndex}..{loop.EndIndex}):" +
                                      $" {rootsTrace};");
            }

            var normalized = Loop.Deserialize(loop.Normalized);
            var normalizedRoots = service.CreateRoots(normalized, loop.NormalizationRoot);
            var normalizedRootsIndices = Enumerable.Range(0, loop.EndIndex - loop.StartIndex + 1)
                .Prepend(-1 + normalized.Length)
                .Select(x => x + loop.NormalizationShift)
                .Select(i => (i % normalized.Length) + 1)
                .ToList();
            var normalizedRecreation = string.Join(" ", normalizedRootsIndices
                .Select(i => $"{normalizedRoots[i]}{(i == 0 ? normalized.Span[0].FromType : normalized.Span[i - 1].ToType).ChordTypeToString()}"));

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

    private void AssertNormalized((byte root1, byte root2) roots, (int rootIndex1, int rootIndex2) rootIndices, LoopSelfJumpBlock loopSelfJumpBlocks)
    {
        Assert.Equal(roots, ToNormalizedRoots(
            loopSelfJumpBlocks.JumpPoints!.Value,
            loopSelfJumpBlocks.Normalized,
            loopSelfJumpBlocks.Loop1.NormalizationRoot,
            loopSelfJumpBlocks.Loop2.NormalizationRoot));
        Assert.Equal(rootIndices, loopSelfJumpBlocks.JumpPoints);
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