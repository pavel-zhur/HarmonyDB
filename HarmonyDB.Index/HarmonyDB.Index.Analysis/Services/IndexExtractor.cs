using System.Runtime.InteropServices;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Index;
using HarmonyDB.Index.Analysis.Models.Index.Blocks;
using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;
using HarmonyDB.Index.Analysis.Models.Index.Graphs;
using HarmonyDB.Index.Analysis.Models.Index.Graphs.Interfaces;
using HarmonyDB.Index.Analysis.Models.Structure;
using HarmonyDB.Index.Analysis.Tools;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Services;

public class IndexExtractor
{
    public (List<PolySequenceBlock> polySequences, List<PolyLoopBlock> polyLoops) FindPolyBlocks(
        ReadOnlyMemory<CompactHarmonyMovement> sequence, 
        IReadOnlyList<byte> roots, 
        IReadOnlyList<LoopBlock> loops,
        bool extractPolySequences,
        bool extractPolyLoops,
        PolyBlocksExtractionParameters extractionParameters)
    {
        if (!extractPolySequences && !extractPolyLoops) throw new ArgumentException();

        var successiveBreaksForSequences = new bool[sequence.Length + 1];
        if (extractionParameters.SequencesExcludeIfContainsSuccessiveLoopsBreak)
        {
            foreach (var i in loops
                         .Where(x => x.SuccessionsSignificant)
                         .SelectMany(l => Enumerable.Range(l.StartIndex + l.LoopLength, l.EndIndex - l.StartIndex - 2 * l.LoopLength + 2)))
            {
                successiveBreaksForSequences[i] = true;
            }
        }

        var successiveBreaksForLoops = new List<(int startIndex, int endIndex)>();
        if (extractionParameters.LoopsExcludeIfContainsSuccessiveLoopsBreak)
        {
            foreach (var loop in loops
                         .Where(x => x.SuccessionsSignificant))
            {
                if (loop.Successions == 2)
                {
                    successiveBreaksForLoops.Add((loop.StartIndex, loop.EndIndex));
                }
                else
                {
                    successiveBreaksForLoops.Add((loop.StartIndex, loop.StartIndex + loop.LoopLength - 1));
                    successiveBreaksForLoops.Add((loop.EndIndex - loop.LoopLength + 1, loop.EndIndex));
                }
            }
        }

        var sequenceLength = sequence.Length;
        var subsequencePositions = new Dictionary<string, List<(int StartIndex, int EndIndex)>>();
        var subsequenceNormalizations = new Dictionary<(int StartIndex, int EndIndex), string>();
        var subsequenceOverLoops = new Dictionary<string, (int length, int sublength)>();
        var subsequenceOverLoopsWithTails = new Dictionary<string, (int length, int sublength)>();
        var subsequenceLoops = new HashSet<string>();

        // Identify all possible subsequences
        for (var length = 1; length <= sequenceLength; length++)
        {
            for (var startIndex = 0; startIndex <= sequenceLength - length; startIndex++)
            {
                var endIndex = startIndex + length - 1;
                var subsequence = sequence.Slice(startIndex, length);
                var normalized = subsequence.SerializeLoop();
                var position = (startIndex, endIndex);

                // find perfect loops (A, A, ... A), with and without tails
                for (var subLength = 2; subLength < length; subLength++)
                {
                    if (roots[startIndex] != roots[startIndex + subLength]
                        || sequence.Span[startIndex].FromType != sequence.Span[startIndex + subLength - 1].ToType)
                    {
                        continue;
                    }
                    
                    if (Enumerable
                            .Range(0, length / subLength)
                            .Select(i => roots[startIndex + i * subLength])
                            .Distinct()
                            .Count() != 1
                        || Enumerable
                            .Range(0, length / subLength)
                            .Select(i => subsequenceNormalizations[(startIndex + i * subLength, startIndex + (i + 1) * subLength - 1)])
                            .Distinct()
                            .Count() != 1)
                    {
                        continue;
                    }

                    var modulus = length % subLength;
                    
                    if (modulus == 0)
                    {
                        subsequenceOverLoops[normalized] = (length, subLength);
                        break;
                    }

                    if (roots[startIndex] == roots[endIndex - modulus + 1]
                        && subsequenceNormalizations[(startIndex, startIndex + modulus - 1)] == subsequenceNormalizations[(endIndex - modulus + 1, endIndex)])
                    {
                        subsequenceOverLoopsWithTails[normalized] = (length, subLength);
                        break;
                    }
                }
                
                // find loops
                if (roots[position.startIndex] == roots[position.endIndex + 1] && sequence.Span[position.startIndex].FromType == sequence.Span[position.endIndex].ToType)
                {
                    subsequenceLoops.Add(normalized);
                }
                
                if (!subsequencePositions.ContainsKey(normalized))
                {
                    subsequencePositions[normalized] = new();
                }

                subsequencePositions[normalized].Add(position);

                subsequenceNormalizations[position] = normalized;
            }
        }

        // Find poly loops
        var loopNormalizations = new Dictionary<string, (string normalized, int normalizationShift)>();
        var polyLoops = !extractPolyLoops
            ? new()
            : subsequenceOverLoops
                .Concat(subsequenceOverLoopsWithTails)
                .SelectMany(x => subsequencePositions[x.Key].Select(p => (n: x.Key, p, x.Value.length, x.Value.sublength)))
                .Concat(subsequenceLoops.Except(subsequenceOverLoops.Keys.Concat(subsequenceOverLoopsWithTails.Keys)).SelectMany(n => subsequencePositions[n].Select(p =>
                {
                    var length = p.EndIndex - p.StartIndex + 1;
                    return (n, p, length, sublength: length);
                })))
                .Where(p => !extractionParameters.LoopsExcludeIfContainsSuccessiveLoopsBreak
                    || !successiveBreaksForLoops.Any(b => b.startIndex.IsBetween(p.p.StartIndex, p.p.EndIndex) && b.endIndex.IsBetween(p.p.StartIndex, p.p.EndIndex))
                    && !loops.Any(l => p.p.StartIndex.IsBetween(l.StartIndex, l.EndIndex) && p.p.EndIndex.IsBetween(l.StartIndex, l.EndIndex)))
                .GroupBy(x => 
                    (loopNormalizations.TryGetValue(x.n, out var normalization)
                        ? normalization
                        : loopNormalizations[x.n] = (
                        GetNormalizedProgression(sequence.Slice(x.p.StartIndex, x.sublength), out var normalizationShift).SerializeLoop(), 
                        normalizationShift)).normalized)
                .Where(g => !extractionParameters.LoopsExcludeIfContainsAllChordsMoreThanOnce
                    || !ContainsAllChordsMoreThanOnce(sequence.Slice(g.First().p.StartIndex, g.First().sublength)))
                .Select(g => g
                    .Where(x => x.length == x.sublength)
                    .GroupBy(x => x.p.StartIndex)
                    .OrderBy(g => g.Key)
                    .WithPrevious()
                    .Where(x => x.previous?.Key + 1 != x.current.Key)
                    .SelectMany(x => x.current)
                    .Select(x => (start: x, max: g.Where(y => y.p.StartIndex == x.p.StartIndex).MaxBy(x => x.length)))
                    .Select(x => new PolyLoopBlock
                    {
                        Normalized = g.Key,
                        NormalizationShift = loopNormalizations[x.start.n].normalizationShift,
                        NormalizationRoot = GetLoopNormalizationRoot(roots, x.start.p.StartIndex, loopNormalizations[x.start.n].normalizationShift, x.start.length),
                        StartIndex = x.start.p.StartIndex,
                        EndIndex = x.max.p.EndIndex,
                        Loop = sequence.Slice(x.start.p.StartIndex, x.start.sublength),
                    })
                    .ToList())
                .Where(g => g.Count > 1 || g is [ { SuccessionsSignificant: true } ])
                .SelectMany(x => x)
                .ToList();

        if (extractionParameters.SequencesExcludeIfContainsSuccessiveLoopsBreak)
        {
            subsequencePositions = subsequencePositions.ToDictionary(
                x => x.Key,
                x => x.Value
                    .Where(p =>
                        Enumerable
                            .Range(p.StartIndex + 1, p.EndIndex - p.StartIndex)
                            .All(i => !successiveBreaksForSequences[i]))
                    .ToList());
        }
        
        // Create poly sequences
        var polySequences = !extractPolySequences
            ? new()
            : subsequencePositions
                .Where(x => x.Value.Count > 1) // occurs more than once
                .Where(x => !extractionParameters.SequencesExcludeIfOverLoops || !subsequenceOverLoops.ContainsKey(x.Key))
                .Where(x => !extractionParameters.SequencesExcludeIfOverLoopsWithTails || !subsequenceOverLoopsWithTails.ContainsKey(x.Key))
                .Where(x => !extractionParameters.SequencesExcludeIfContainsAllChordsMoreThanOnce
                            || !ContainsAllChordsMoreThanOnce(sequence[x.Value.First().StartIndex..(x.Value.First().EndIndex + 1)]))
                .Select(x => x.Key)
                .Where(
                    x => // may not be extended without losing occurrences (unless it overlaps with itself when steps right)
                    {
                        var positions = subsequencePositions[x];
                        var position = positions[0];

                        if (extractionParameters.SequencesExcludeIfExtendsLeftWithoutLosingOccurrences
                            && positions.All(p => p.StartIndex > 0 && (!extractionParameters.SequencesExcludeIfContainsSuccessiveLoopsBreak || !successiveBreaksForSequences[p.StartIndex]))
                            && subsequencePositions[subsequenceNormalizations[(position.StartIndex - 1, position.EndIndex)]].Count == positions.Count)
                            return false;

                        if (extractionParameters.SequencesExcludeIfExtendsRightWithoutLosingOccurrencesOrOverlappingWithItself
                            && positions.All(p => p.EndIndex < sequenceLength - 1 && (!extractionParameters.SequencesExcludeIfContainsSuccessiveLoopsBreak || !successiveBreaksForSequences[p.EndIndex]))
                            && !positions.Any(x => (position.EndIndex + 1).IsBetween(x.StartIndex, x.EndIndex))
                            && subsequencePositions[subsequenceNormalizations[(position.StartIndex, position.EndIndex + 1)]].Count == positions.Count)
                            return false;

                        return true;
                    })
                .SelectMany(n => subsequencePositions[n]
                    .Select(p => (p, c: new List<PolySequenceBlock>()))
                    .Select(p => (n, p.c, p.p, s: new PolySequenceBlock
                    {
                        StartIndex = p.p.StartIndex,
                        EndIndex = p.p.EndIndex,
                        Normalized = n,
                        NormalizationRoot = roots[p.p.StartIndex],
                        Sequence = sequence.Slice(p.p.StartIndex, p.p.EndIndex - p.p.StartIndex + 1),
                        Children = new List<PolySequenceBlock>(),
                    })))
                .ToDictionary(x => x.p);

        // Detect self overlaps
        foreach (var group in polySequences.Select(x => x.Value.s).Concat(polyLoops.Cast<IPolyBlock>()).GroupBy(x => (x.Normalized, x.NormalizationRoot)))
        {
            var open = 0;
            if (group
                .SelectMany(x =>
                    (x: x.StartIndex, isStart: true)
                    .Once()
                    .Append((x: x.EndIndex, isStart: false)))
                .OrderBy(x => x.x)
                .ThenByDescending(x => x.isStart)
                .Any(p => (open += (p.isStart ? 1 : -1)) > 1))
            {
                foreach (var polySequenceBlock in group)
                {
                    polySequenceBlock.SelfOverlapsDetected = true;
                }
            }
        }

        // Fill poly sequences children
        foreach (var (_, children, position, _) in polySequences.Values)
        {
            children.AddRange(polySequences
                .Where(x => x.Key.StartIndex.IsBetween(position.StartIndex, position.EndIndex) && x.Key.EndIndex.IsBetween(position.StartIndex, position.EndIndex))
                .Select(x => x.Value.s));
        }

        return (polySequences.Select(x => x.Value.s).ToList(), polyLoops);
    }

    private bool ContainsAllChordsMoreThanOnce(ReadOnlyMemory<CompactHarmonyMovement> sequence)
    {
        byte root = 0;
        var chords = new Dictionary<(byte root, ChordType chordType), int>
        {
            { (root, sequence.Span[0].FromType), 1 },
        };

        foreach (var movement in MemoryMarshal.ToEnumerable(sequence))
        {
            var chord = (root = Note.Normalize(root + movement.RootDelta), movement.ToType);
            chords[chord] = chords.GetValueOrDefault(chord, 0) + 1;
        }

        return chords.Any(x => x.Value == 1);
    }

    public List<byte> CreateRoots(ReadOnlyMemory<CompactHarmonyMovement> sequence, byte firstRoot)
        => firstRoot
            .Once()
            .Concat(MemoryMarshal.ToEnumerable(sequence)
                .Select(d => firstRoot = Note.Normalize(d.RootDelta + firstRoot)))
            .ToList();

    public List<PingPongBlock> FindPingPongs(BlockGraph graph)
        => graph.Joints
            .SelectMany(l => l.Block2.RightJoints.Select(r => (l, r)))
            .Where(x => x.l.Block1.Block.Type == x.r.Block2.Block.Type
                        && x.l.Block1.Block.Normalized == x.r.Block2.Block.Normalized
                        && x.l.Block1.Block.NormalizationRoot == x.r.Block2.Block.NormalizationRoot)
            .GroupBy(x =>
            {
                var normalizations = x.l.Normalization.Once().Append(x.r.Normalization).OrderBy(x => x).ToList();
                return normalizations[0] + normalizations[1];
            })
            .SelectMany(pingPongsOfTheNormalization => pingPongsOfTheNormalization
                // find consecutive joints, each subgroup will become a ping pong
                .WithPrevious()
                .ToChunksByShouldStartNew(p => p.current.l != p.previous?.r)
                .Select(c => c.Select(p => p.current)))
            .Select(x =>
            {
                var children = x.SelectMany(x => x.l.Block1.Block.Once().Append(x.r.Block1.Block).Append(x.r.Block2.Block)).Distinct().OrderBy(x => x.StartIndex).ToList();
                if (children.Select(x => x.StartIndex).AnyDuplicates()
                    || children.Select(x => x.EndIndex).AnyDuplicates())
                    throw new("Unique blocks start indices expected.");
                return new PingPongBlock
                {
                    Children = children,
                };
            })
            .ToList();

    public List<LoopBlock> FindSimpleLoops(ReadOnlyMemory<CompactHarmonyMovement> sequence, IReadOnlyList<byte> roots)
    {
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
            if (loopStartRoot != roots[movementIndex + 1])
                throw new("Could not have happened.");

            if((movementIndexToLoopStart == -1 ? sequence.Span[0].FromType : sequence.Span[movementIndexToLoopStart].ToType) != sequence.Span[movementIndex].ToType)
                throw new("Could not have happened.");

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

            var normalized = GetNormalizedProgression(foundLoop, out var normalizationShift, out _).SerializeLoop();
            loops.Add(new()
            {
                Loop = foundLoop,
                Normalized = normalized,
                NormalizationShift = normalizationShift,
                NormalizationRoot = GetLoopNormalizationRoot(roots, movementIndexToLoopStart + 1, normalizationShift, foundLoop.Length),
                StartIndex = movementIndexToLoopStart + 1,
                EndIndex = movementIndex,
            });

            movementIndex -= foundLoop.Length - 1;
            indices.Clear();
        }

        return loops;
    }
    
    private byte GetLoopNormalizationRoot(IReadOnlyList<byte> roots, int startIndex, int normalizationShift, int loopLength)
        => roots[startIndex + InvertNormalizationShift(normalizationShift, loopLength)];

    public List<LoopSelfMultiJumpBlock> FindSelfJumps(ReadOnlyMemory<CompactHarmonyMovement> sequence, IReadOnlyList<LoopBlock> loops)
        => loops
            .WithIndices()
            .GroupBy(x => x.x.Normalized)
            .SelectMany(g => g
                .WithPrevious()
                .ToChunks(x => x.previous.HasValue && (x.current.i - x.previous.Value.i) switch
                {
                    > 2 => false,
                    < 1 => throw new("Could not have happened, indices are ascending."),
                    _ => x.current.x.StartIndex - x.previous.Value.x.EndIndex < 3,
                })
                .Where(c => c.criterium)
                .Select(c => (
                    normalized: g.Key,
                    indices: c.chunk.First().previous!.Value.i.Once().Concat(c.chunk.Select(x => x.current.i))
                        .ToList())))
            .Select(j =>
            {
                var firstLoop = loops[j.indices[0]];
                return new LoopSelfMultiJumpBlock
                {
                    ChildLoops = Enumerable.Range(j.indices[0], j.indices.Max() - j.indices[0] + 1).Select(x => loops[x]).ToList(),
                    NormalizationRootsFlow = j.indices
                        .Select(x => loops[x])
                        .Where(x => x.Normalized == firstLoop.Normalized)
                        .Select(x => x.NormalizationRoot)
                        .ToChunks(x => x)
                        .Select(x => x.criterium)
                        .ToList(),
                    ChildJumps = j.indices
                        .Where(x => loops[x].Normalized == firstLoop.Normalized)
                        .AsPairs()
                        .Select(p =>
                        {
                            var previousLoop = loops[p.previous];
                            var currentLoop = loops[p.current];

                            var jointLoop = p.previous == p.current - 1 ? null : loops[p.current - 1];
                            var loopsGap = currentLoop.StartIndex - previousLoop.EndIndex;

                            // 1 means sequential, 2 means one movement in between
                            if (loopsGap > 2)
                                throw new ArgumentOutOfRangeException(nameof(loopsGap), loopsGap, "The gap cannot be more than 1.");

                            var result = new LoopSelfJumpBlock
                            {
                                Loop1 = previousLoop,
                                Loop2 = currentLoop,
                                JointLoop = jointLoop,
                                JointMovement = loopsGap switch
                                {
                                    2 => sequence.Span[previousLoop.EndIndex + 1],
                                    1 => null, // single ambiguous chord
                                    < 1 => null, // multiple ambiguous chords, overlap
                                    _ => throw new ArgumentOutOfRangeException()
                                },
                            };

                            return result;
                        })
                        .ToList(),
                };
            })
            .ToList();

    public List<SequenceBlock> FindSequenceBlocks(ReadOnlyMemory<CompactHarmonyMovement> sequence, IReadOnlyList<IBlock> existingBlocks, IReadOnlyList<byte> roots)
        => Enumerable
            .Range(0, sequence.Length)
            .Except(existingBlocks.OfType<IIndexedBlock>().SelectMany(b => Enumerable.Range(b.StartIndex, b.BlockLength)))
            .OrderBy(x => x)
            .WithPrevious()
            .ToChunksByShouldStartNew(x => x.previous + 1 != x.current)
            .Select(c =>
            {
                var startIndex = c[0].current;
                var endIndex = c[^1].current;

                var subsequence = sequence.Slice(startIndex, endIndex - startIndex + 1);

                return new SequenceBlock
                {
                    StartIndex = startIndex,
                    EndIndex = endIndex,
                    Sequence = subsequence,
                    Normalized = subsequence.SerializeLoop(),
                    NormalizationRoot = roots[startIndex],
                };
            })
            .ToList();

    public List<LoopMassiveOverlapsBlock> FindMassiveOverlapsBlocks(IReadOnlyList<LoopBlock> loopBlocks)
    {
        var result = new List<LoopMassiveOverlapsBlock>();
        var currentOpenLoops = new List<LoopBlock>();
        List<LoopBlock>? currentMassiveOverlapsBlock = null;
        foreach (var (i, isStart, loop) in loopBlocks
                     .SelectMany(x => new[]
                     {
                         (i: x.StartIndex, isStart: true, loop: x),
                         (i: x.EndIndex, isStart: false, loop: x),
                     })
                     .OrderBy(x => x.i)
                     .ThenByDescending(x => x.isStart))
        {
            if (isStart)
            {
                currentOpenLoops.Add(loop);
            }

            if (currentOpenLoops.Count > 2)
            {
                if (currentMassiveOverlapsBlock == null)
                {
                    currentMassiveOverlapsBlock = currentOpenLoops.ToList();
                }
                else if (!currentMassiveOverlapsBlock.Contains(loop))
                    currentMassiveOverlapsBlock.Add(loop);
            }

            if (!isStart)
            {
                currentOpenLoops.Remove(loop);

                if (currentOpenLoops.Count == 1 && currentMassiveOverlapsBlock != null)
                {
                    var first = currentMassiveOverlapsBlock[0];
                    var last = currentMassiveOverlapsBlock[^1];
                    currentMassiveOverlapsBlock.RemoveAt(0);
                    currentMassiveOverlapsBlock.RemoveAt(currentMassiveOverlapsBlock.Count - 1);
                    result.Add(new()
                    {
                        StartIndex = first.StartIndex,
                        EndIndex = last.EndIndex,
                        Edge1 = first,
                        Edge2 = last,
                        InternalLoops = currentMassiveOverlapsBlock,
                    });

                    currentMassiveOverlapsBlock = null;
                }
            }
        }

        if (currentOpenLoops.Count != 0 || currentMassiveOverlapsBlock != null)
        {
            throw new("Could not have happened.");
        }

        return result;
    }

    public List<IBlock> FindBlocks(ReadOnlyMemory<CompactHarmonyMovement> sequence, IReadOnlyList<byte> roots, IReadOnlyList<BlockType> blockTypes, PolyBlocksExtractionParameters? polyBlocksExtractionParameters = null)
    {
        List<IBlock> blocks = new();

        if (blockTypes.Contains(BlockType.Loop))
        {
            var loops = FindSimpleLoops(sequence, roots);
            blocks.AddRange(loops);

            if (blockTypes.Contains(BlockType.LoopSelfJump) || blockTypes.Contains(BlockType.LoopSelfMultiJump))
            {
                var jumps = FindSelfJumps(sequence, loops);
                if (blockTypes.Contains(BlockType.LoopSelfMultiJump))
                {
                    blocks.AddRange(jumps);
                }

                if (blockTypes.Contains(BlockType.LoopSelfJump))
                {
                    blocks.AddRange(jumps.SelectMany(x => x.ChildJumps));
                }
            }

            if (blockTypes.Contains(BlockType.MassiveOverlaps))
            {
                blocks.AddRange(FindMassiveOverlapsBlocks(loops));
            }

            var extractPolySequences = blockTypes.Contains(BlockType.PolySequence);
            var extractPolyLoops = blockTypes.Contains(BlockType.PolyLoop);
            if (extractPolySequences || extractPolyLoops)
            {
                var (polySequences, polyLoops) = FindPolyBlocks(sequence, roots, loops, extractPolySequences, extractPolyLoops, polyBlocksExtractionParameters ?? new());
                if (extractPolySequences)
                {
                    blocks.AddRange(polySequences);
                }

                if (extractPolyLoops)
                {
                    blocks.AddRange(polyLoops);
                }
            }
        }

        if (blockTypes.Contains(BlockType.Sequence))
        {
            blocks.AddRange(FindSequenceBlocks(sequence, blocks, roots));
        }

        if (blockTypes.Contains(BlockType.PingPong))
        {
            var graph = CreateGraph(blocks);
            blocks.AddRange(FindPingPongs(graph));
        }

        if (blockTypes.Contains(BlockType.SequenceStart))
        {
            blocks.Add(new EdgeBlock(BlockType.SequenceStart, sequence.Length));
        }

        if (blockTypes.Contains(BlockType.SequenceEnd))
        {
            blocks.Add(new EdgeBlock(BlockType.SequenceEnd, sequence.Length));
        }

        blocks.Sort((block1, block2) => block1.StartIndex.CompareTo(block2.StartIndex) switch
        {
            var x when x != 0 => x,
            _ when block1 == block2 => 0,
            _ => -block1.EndIndex.CompareTo(block2.EndIndex),
        });

        return blocks;
    }

    private static IEnumerable<IIndexedBlock> GetChildBlocksSubtree(IIndexedBlock block) =>
        block.Children.SelectMany(b => GetChildBlocksSubtree(b).Prepend(b));

    public BlockGraph CreateGraph(IReadOnlyList<IBlock> blocks)
    {
        var environments = blocks.OfType<IIndexedBlock>().Select(b => new BlockEnvironment
        {
            Block = b,
        }).ToDictionary(x => x.Block);
        
        foreach (var environment in environments.Values)
        {
            environment.Children.AddRange(environment.Block.Children.Select(c => environments[c]));
            environment.ChildrenSubtree.AddRange(GetChildBlocksSubtree(environment.Block).Select(c => environments[c]).Distinct());
        }

        foreach (var (p, c) in environments.Values.SelectMany(p => p.ChildrenSubtree.Select(c => (p, c))))
        {
            c.Parents.Add(p);
        }
        
        var joints = environments.Values.SelectMany((b1, i1) => environments.Values
                .Where((b2, i2) =>
                    i2 > i1 
                    && !(b1.Block.StartIndex > b2.Block.EndIndex + 1 || b2.Block.StartIndex > b1.Block.EndIndex + 1) 
                    && !b1.ChildrenSubtree.Contains(b2) 
                    && !b2.ChildrenSubtree.Contains(b1)
                    
                    // neither of them is fully within another
                    && new[] { b1.Block.StartIndex, b1.Block.EndIndex, }.Any(x => !x.IsBetween(b2.Block.StartIndex, b2.Block.EndIndex))
                    && new[] { b2.Block.StartIndex, b2.Block.EndIndex, }.Any(x => !x.IsBetween(b1.Block.StartIndex, b1.Block.EndIndex)))
                .Select(b2 => b1.Block.StartIndex.CompareTo(b2.Block.StartIndex) switch
                {
                    -1 => (b1, b2),
                    1 => (b1: b2, b2: b1),
                    0 => throw new("The blocks starts may not be the same."),
                    _ => throw new ArgumentOutOfRangeException(),
                }))
            .Select(x => new BlockJoint
            {
                Block1 = x.b1,
                Block2 = x.b2,
            })
            .ToList();
        
        foreach (var joint in joints)
        {
            joint.Block1.RightJoints.Add(joint);
            joint.Block2.LeftJoints.Add(joint);
        }

        var uselessLoops = environments.Values.Where(e =>
        {
            if (e.Block is not LoopBlock)
                return false;

            var left = e.LeftJoints.Select(x => x.Block1.Block).OfType<LoopBlock>().ToList();
            var right = e.RightJoints.Select(x => x.Block2.Block).OfType<LoopBlock>().ToList();
            return left.Count == 1
                   && right.Count == 1
                   && left.Single().EndIndex + 1 >= right.Single().StartIndex;
        });
        
        foreach (var environment in uselessLoops)
        {
            environment.Detections |= BlockDetections.UselessLoop;
        }

        return new()
        {
            Environments = environments.ToDictionary(x => x.Key, x => (IBlockEnvironment)x.Value),
            Joints = joints,
        };
    }

    /// <returns>
    /// Between 0 (inclusive) and progression length (exclusive).
    /// Inverts the number of steps one progression is ahead to the number of steps it is behind,
    /// where a progression may be the normalized progression or the original progression.
    /// Unaware of invariants.
    /// </returns>
    public int InvertNormalizationShift(int normalizationShift, int progressionLength) =>
        (progressionLength - normalizationShift) % progressionLength;

    public ReadOnlyMemory<CompactHarmonyMovement> GetNormalizedProgression(ReadOnlyMemory<CompactHarmonyMovement> progression)
        => GetNormalizedProgression(progression, out _, out _);

    /// <param name="normalizationShift">
    /// Between 0 (inclusive) and progression length (exclusive).
    /// Returns the index of the start of the original progression in the normalized progression.
    /// In other words, the number of steps the original progression is ahead or the normalized progression is behind.
    /// For invariants > 1, returns the minimal possible shift, i.e. between 0 (inclusive) and (progression length / invariants) (exclusive).
    /// </param>
    public ReadOnlyMemory<CompactHarmonyMovement> GetNormalizedProgression(ReadOnlyMemory<CompactHarmonyMovement> progression, out int normalizationShift)
        => GetNormalizedProgression(progression, out normalizationShift, out _);

    /// <param name="normalizationShift">
    /// Between 0 (inclusive) and progression length (exclusive).
    /// Returns the index of the start of the original progression in the normalized progression.
    /// In other words, the number of steps the original progression is ahead or the normalized progression is behind.
    /// For invariants > 1, returns the minimal possible shift, i.e. between 0 (inclusive) and (progression length / invariants) (exclusive).
    /// </param>
    public ReadOnlyMemory<CompactHarmonyMovement> GetNormalizedProgression(ReadOnlyMemory<CompactHarmonyMovement> progression, out int normalizationShift, out int invariants)
    {
        var buffer = new byte[4];
        var idSequences = MemoryMarshal.ToEnumerable(progression)
            .Select(p =>
            {
                buffer[0] = p.RootDelta;
                buffer[1] = (byte)p.FromType;
                buffer[2] = (byte)p.ToType;
                return BitConverter.ToInt32(buffer);
            })
            .ToArray();

        var length = progression.Length;
        var shifts = Enumerable.Range(0, length).ToList();
        var iteration = 0;
        invariants = 1;
        while (shifts.Count > 1)
        {
            if (iteration == length) // multiple shifts possible
            {
                invariants = shifts.Count;
                break;
            }

            shifts = shifts
                .GroupBy(s => idSequences[(s + iteration) % length])
                .MinBy(g => g.Key)!
                .ToList();

            iteration++;
        }

        normalizationShift = shifts
            .Select(x => InvertNormalizationShift(x, length))
            .Min();
        return Enumerable.Range(shifts.First(), length)
            .Select(s => progression.Span[s % length])
            .ToArray();
    }

    public List<StructureLink> FindStructureLinks(string externalId, CompactChordsProgression compactChordsProgression)
    {
        var loopResults = new Dictionary<(string normalized, byte normalizationRoot), (float occurrences, float successions, short length)>();
        foreach (var extendedHarmonyMovementsSequence in compactChordsProgression.ExtendedHarmonyMovementsSequences)
        {
            var roots = CreateRoots(extendedHarmonyMovementsSequence.Movements, extendedHarmonyMovementsSequence.FirstRoot);
            var loops = FindSimpleLoops(extendedHarmonyMovementsSequence.Movements, roots);

            foreach (var loop in loops)
            {
                var key = (loop.Normalized, loop.NormalizationRoot);
                var counters = loopResults.GetValueOrDefault(key);
                loopResults[key] = ((float occurrences, float successions, short length))(
                    counters.occurrences + loop.Occurrences,
                    counters.successions + loop.Successions,
                    counters.length + loop.BlockLength);
            }
        }

        var items = loopResults
            .Select(p => new StructureLink(
                p.Key.normalized,
                externalId,
                p.Key.normalizationRoot,
                p.Value.occurrences,
                p.Value.successions,
                (float)p.Value.length / compactChordsProgression.ExtendedHarmonyMovementsSequences.Sum(s => s.Movements.Length)))
            .ToList();
        return items;
    }
}