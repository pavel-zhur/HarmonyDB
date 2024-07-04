using System.Runtime.InteropServices;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Index;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Services;

public class IndexExtractor
{
    public List<byte> CreateRoots(ReadOnlyMemory<CompactHarmonyMovement> sequence, byte firstRoot)
        => firstRoot
            .Once()
            .Concat(MemoryMarshal.ToEnumerable(sequence)
                .Select(d => firstRoot = Note.Normalize(d.RootDelta + firstRoot)))
            .ToList();

    public List<LoopBlock> FindSimpleLoops(ReadOnlyMemory<CompactHarmonyMovement> sequence, byte firstRoot)
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

            var normalized = Loop.Serialize(Loop.GetNormalizedProgression(foundLoop, out var normalizationShift, out _));
            loops.Add(new()
            {
                Loop = foundLoop,
                Normalized = normalized,
                NormalizationShift = normalizationShift,
                NormalizationRoot = roots[movementIndexToLoopStart + 1 + Loop.InvertNormalizationShift(normalizationShift, foundLoop.Length)],
                StartIndex = movementIndexToLoopStart + 1,
                EndIndex = movementIndex,
            });

            movementIndex -= foundLoop.Length - 1;
            indices.Clear();
        }

        return loops;
    }

    public List<LoopSelfMultiJumpBlock> FindSelfJumps(ReadOnlyMemory<CompactHarmonyMovement> sequence, List<LoopBlock> loops)
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
                        .WithPrevious()
                        .Where(p => p.previous.HasValue)
                        .Select(p =>
                        {
                            var previousLoop = loops[p.previous!.Value];
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
}