using System.Runtime.InteropServices;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Index;
using HarmonyDB.Index.Analysis.Models.Structure;
using HarmonyDB.Index.Analysis.Tools;
using OneShelf.Common;
using static System.Reflection.Metadata.BlobBuilder;

namespace HarmonyDB.Index.Analysis.Services;

public class IndexExtractor
{
    public List<byte> CreateRoots(ReadOnlyMemory<CompactHarmonyMovement> sequence, byte firstRoot)
        => firstRoot
            .Once()
            .Concat(MemoryMarshal.ToEnumerable(sequence)
                .Select(d => firstRoot = Note.Normalize(d.RootDelta + firstRoot)))
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
                NormalizationRoot = roots[movementIndexToLoopStart + 1 + InvertNormalizationShift(normalizationShift, foundLoop.Length)],
                StartIndex = movementIndexToLoopStart + 1,
                EndIndex = movementIndex,
            });

            movementIndex -= foundLoop.Length - 1;
            indices.Clear();
        }

        return loops;
    }

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

    public List<IBlock> FindAnyLoops(ReadOnlyMemory<CompactHarmonyMovement> sequence, IReadOnlyList<byte> roots, BlocksExtractionLogic blocksExtractionLogic)
    {
        var loops = FindSimpleLoops(sequence, roots);

        switch (blocksExtractionLogic)
        {
            case BlocksExtractionLogic.Loops:
                return loops.Cast<IBlock>().ToList();
            
            case BlocksExtractionLogic.ReplaceWithSelfJumps:
            {
                var jumps = FindSelfJumps(sequence, loops).SelectMany(x => x.ChildJumps).ToList();
                var involvedLoops = jumps
                    .SelectMany(x => new[] { x.Loop1, x.Loop2, x.JointLoop, })
                    .Where(x => x != null)
                    .Distinct();

                return loops.Except(involvedLoops).Cast<IBlock>().Concat(jumps).ToList();
            }

            case BlocksExtractionLogic.ReplaceWithSelfMultiJumps:
            {
                var jumps = FindSelfJumps(sequence, loops);
                var involvedLoops = jumps
                    .SelectMany(x => x.ChildLoops)
                    .Distinct();

                return loops.Except(involvedLoops).Cast<IBlock>().Concat(jumps).ToList();
            }

            case BlocksExtractionLogic.All:
            {
                var jumps = FindSelfJumps(sequence, loops);
                var massiveOverlaps = FindMassiveOverlapsBlocks(loops);
                return jumps.Cast<IBlock>()
                    .Concat(jumps.SelectMany(x => x.ChildJumps))
                    .Concat(loops)
                    .Concat(massiveOverlaps)
                    .ToList();
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(blocksExtractionLogic), blocksExtractionLogic, null);
        }
    }

    public List<SequenceBlock> FindSequenceBlocks(ReadOnlyMemory<CompactHarmonyMovement> sequence, IReadOnlyList<IBlock> existingBlocks, IReadOnlyList<byte> roots)
        => Enumerable
            .Range(0, sequence.Length)
            .Except(existingBlocks.SelectMany(b => Enumerable.Range(b.StartIndex, b.BlockLength)))
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
                    NormalizationRoot = roots[startIndex + 1],
                };
            })
            .ToList();

    public List<LoopMassiveOverlapsBlock> FindMassiveOverlapsBlocks(ReadOnlyMemory<CompactHarmonyMovement> sequence,
        IReadOnlyList<LoopBlock> loopBlocks)
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