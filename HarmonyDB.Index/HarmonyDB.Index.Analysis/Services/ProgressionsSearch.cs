using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Interfaces;
using Microsoft.Extensions.Logging;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Services;

public class ProgressionsSearch
{
    private readonly ILogger<ProgressionsSearch> _logger;

    public ProgressionsSearch(ILogger<ProgressionsSearch> logger)
    {
        _logger = logger;
    }

    public HarmonyMovementsSequence? ExtractSearch(ChordsProgression progression, int start, int end)
    {
        if (start >= end) throw new ArgumentOutOfRangeException($"{nameof(start)} must be less than {nameof(end)}.");

        var (_, harmonyGroup1, _, _, extendedMovementsSequence) = progression.OriginalSequence[start];
        var (_, harmonyGroup2, _, _, extendedMovementsSequence2) = progression.OriginalSequence[end];
        if (extendedMovementsSequence == null || extendedMovementsSequence2 == null || extendedMovementsSequence != extendedMovementsSequence2) return null;

        if (harmonyGroup1 == harmonyGroup2) return null;

        var sequence = progression.HarmonySequence
            .SkipWhile(x => x.harmonyGroup != harmonyGroup1)
            .TakeWhile(x => x.harmonyGroup != harmonyGroup2)
            .Select(x => x.harmonyGroup)
            .Append(harmonyGroup2)
            .ToList();

        if (sequence.Count <= 1) throw new("Could not have happened.");

        return ProgressionsBuilder.GetHarmonyMovementsSequences(sequence, ProgressionsBuilder.IsValidExtended).Single();
    }

    public (Dictionary<ChordsProgression, float> foundProgressionsWithCoverage, Dictionary<HarmonyGroup, bool> harmonyGroupsWithIsFirst) Search(IReadOnlyList<ChordsProgression> progressions, HarmonyMovementsSequence searchPhrase)
    {
        var (foundProgressionsWithCoverage, harmonyGroupsWithIsFirst) = Search(progressions.AsEnumerable(), searchPhrase, true);

        return (
            foundProgressionsWithCoverage.ToDictionary(x => (ChordsProgression)x.Key, x => x.Value),
            harmonyGroupsWithIsFirst.ToDictionary(x => ((ChordsProgression)x.Key.progression).HarmonySequence[x.Key.index].harmonyGroup, x => x.Value));
    }

    public Dictionary<ISearchableChordsProgression, float> Search(IEnumerable<ISearchableChordsProgression> progressions, HarmonyMovementsSequence searchPhrase)
        => Search(progressions, searchPhrase, false).foundProgressionsWithCoverage;

    private (Dictionary<ISearchableChordsProgression, float> foundProgressionsWithCoverage, Dictionary<(ISearchableChordsProgression progression, int index), bool> harmonyGroupsWithIsFirst) Search(IEnumerable<ISearchableChordsProgression> progressions, HarmonyMovementsSequence searchPhrase, bool harmonyGroupsToo)
    {
        (Dictionary<ISearchableChordsProgression, float> foundProgressionsWithCoverage, Dictionary<(ISearchableChordsProgression progression, int index), bool> harmonyGroupsWithIsFirst) result = (new(), new());

        var isLoop = searchPhrase.Movements[^1].To.HarmonyRepresentation == searchPhrase.Movements[0].From.HarmonyRepresentation;
        var compactSearchPhrase = searchPhrase.Compact().Movements;

        foreach (var progression in progressions)
        {
            if (result.foundProgressionsWithCoverage.ContainsKey(progression)) continue;

            HashSet<int>? totalFound = null, totalFoundFirsts = null;
            foreach (var extendedHarmonyMovementsSequence in progression.ExtendedHarmonyMovementsSequences)
            {
                var (found, foundFirsts, _) = FindMatchingSubsequences(extendedHarmonyMovementsSequence, compactSearchPhrase, isLoop);
                if (found.Any())
                {
                    totalFound ??= new();
                    totalFoundFirsts ??= new();

                    totalFound.UnionWith(found);

                    if (harmonyGroupsToo)
                    {
                        totalFoundFirsts.UnionWith(foundFirsts);
                    }
                }
            }

            if (totalFound != null)
            {
                result.foundProgressionsWithCoverage.Add(progression, totalFound.Count / (float)progression.HarmonySequenceLength);

                if (harmonyGroupsToo)
                {
                    result.harmonyGroupsWithIsFirst.AddRange(totalFound.Select(i =>
                        new KeyValuePair<(ISearchableChordsProgression progression, int index), bool>(
                            (progression, i), totalFoundFirsts!.Contains(i))), false);
                }
            }
        }
        
        return result;
    }

    private (HashSet<int> found, HashSet<int> foundFirsts, HashSet<int> foundFirstsFulls) FindMatchingSubsequences(ISearchableHarmonyMovementsSequence sequence, ReadOnlyMemory<CompactHarmonyMovement> searchPhrase, bool isLoop)
    {
        if (searchPhrase.IsEmpty) throw new ArgumentOutOfRangeException(nameof(searchPhrase));

        HashSet<int> found = new();
        HashSet<int> foundFirsts = new();
        List<int> foundStarts = new();
        for (var potentialStart = 0; potentialStart < sequence.MovementsCount - searchPhrase.Length + 1; potentialStart++)
        {
            var compatible = true;
            for (var j = 0; j < searchPhrase.Length; j++)
            {
                if (!AreCompatible(sequence.GetMovement(potentialStart + j), searchPhrase.Span[j]))
                {
                    compatible = false;
                    break;
                }
            }

            if (compatible)
            {
                var fromIndex = sequence.FirstMovementFromIndex + potentialStart;
                found.AddRange(Enumerable.Range(fromIndex, searchPhrase.Length + 1));
                foundFirsts.Add(fromIndex);
                foundStarts.Add(potentialStart);
            }
        }

        var foundFirstsFulls = foundFirsts.ToHashSet();
        if (isLoop)
        {
            foreach (var start in foundStarts)
            {
                var lookAt = searchPhrase.Length;
                while (start + lookAt < sequence.MovementsCount
                       && !found.Contains(sequence.FirstMovementFromIndex + start + lookAt + 1)
                       && AreCompatible(sequence.GetMovement(start + lookAt), searchPhrase.Span[lookAt % searchPhrase.Length]))
                {
                    found.Add(sequence.FirstMovementFromIndex + start + lookAt + 1);

                    if (lookAt == searchPhrase.Length)
                    {
                        foundFirsts.Add(sequence.FirstMovementFromIndex + start + lookAt);
                    }

                    lookAt++;
                }

                lookAt = -1;
                while (start + lookAt >= 0
                       && !found.Contains(sequence.FirstMovementFromIndex + start + lookAt)
                       && AreCompatible(sequence.GetMovement(start + lookAt), searchPhrase.Span[lookAt + searchPhrase.Length]))
                {
                    found.Add(sequence.FirstMovementFromIndex + start + lookAt);
                    lookAt--;
                }
            }
        }

        return (found, foundFirsts, foundFirstsFulls);
    }

    private bool AreCompatible(CompactHarmonyMovement sequenceMovement, CompactHarmonyMovement searchPhraseMovement)
    {
        return sequenceMovement.RootDelta == searchPhraseMovement.RootDelta
               && AreCompatible(sequenceMovement.FromType, searchPhraseMovement.FromType)
               && AreCompatible(sequenceMovement.ToType, searchPhraseMovement.ToType);
    }

    private bool AreCompatible(ChordType sequenceMovement, ChordType searchPhraseMovement)
    {
        return sequenceMovement == searchPhraseMovement || searchPhraseMovement == ChordType.Unknown ||
               sequenceMovement == ChordType.Unknown || searchPhraseMovement is not (ChordType.Minor or ChordType.Major) && sequenceMovement is not (ChordType.Minor or ChordType.Major);
    }

    public List<Loop> FindAllLoops(IReadOnlyList<CompactHarmonyMovementsSequence> sequences)
    {
        var roots = sequences
            .Select(x => Enumerable.Range(0, x.Movements.Length).Aggregate(
                new List<int> { x.FirstRoot },
                (result, i) =>
                {
                    result.Add(Note.Normalize(result[^1] + x.Movements.Span[i].RootDelta));
                    return result;
                }))
            .ToList();

        var loopId = 0;
        var startsOfLoopIds = sequences.Select(s => Enumerable.Range(0, s.Movements.Length + 1).Select(_ => new List<int>()).ToList()).ToList();
        var endsOfLoopIds = sequences.Select(s => Enumerable.Range(0, s.Movements.Length + 1).Select(_ => new List<int>()).ToList()).ToList();
        var participationsInLoopIds = sequences.Select(s => Enumerable.Range(0, s.Movements.Length + 1).Select(_ => new HashSet<int>()).ToList()).ToList();
        var loops = new List<Loop>();
        
        for (var r = 0; r < sequences.Count; r++) // in each sequence
        {
            var movements = sequences[r].Movements;
            for (var start = 0; start < movements.Length - 1; start++) // potential starts
            {
                (int id, int shift)? beginsWithKnownId = null;
                for (var endMovement = start + 1; endMovement <= movements.Length - 1; endMovement++) // potential ends
                {
                    if (roots[r][start] == roots[r][endMovement + 1] && movements.Span[start].FromType == movements.Span[endMovement].ToType) // if is a loop
                    {
                        var searchPhraseLength = endMovement - start + 1;

                        // if such loop is already identified (no shift), continue
                        var startWithNoShiftCheck = startsOfLoopIds[r][start].Where(id => loops[id].EndMovement - loops[id].Start + 1 == searchPhraseLength).Select(x => (int?)x).SingleOrDefault();
                        if (startWithNoShiftCheck.HasValue)
                        {
                            beginsWithKnownId ??= (startWithNoShiftCheck.Value, 0);
                            continue;
                        }

                        var searchPhrase = movements.Slice(start, endMovement - start + 1);

                        // if such loop is already identified, possibly with a shift, continue
                        int? foundShift = null;
                        var foundLoop = loops.WithIndices().Where(l =>
                            {
                                if (l.x.EndMovement - l.x.Start != endMovement - start) return false;
                                var shift = CompactHarmonyMovement.AreEqual(searchPhrase, l.x.Progression);
                                foundShift ??= shift;
                                return shift.HasValue;
                            })
                            .AsNullable()
                            .SingleOrDefault();

                        if (foundLoop.HasValue)
                        {
                            beginsWithKnownId ??= (foundLoop.Value.i, foundShift!.Value);
                            continue;
                        }

                        if (beginsWithKnownId.HasValue) // remove repetitions of type A A A A A A
                        {
                            var subLength = loops[beginsWithKnownId.Value.id].EndMovement - loops[beginsWithKnownId.Value.id].Start + 1;
                            var repetitions = searchPhraseLength / subLength;
                            if (searchPhraseLength % subLength == 0
                                && Enumerable
                                    .Range(1, repetitions - 1)
                                    .All(x => CompactHarmonyMovement.AreEqual(searchPhrase.Slice(x & subLength, subLength),
                                        loops[beginsWithKnownId.Value.id].Progression,
                                        fixedShift: beginsWithKnownId.Value.shift).HasValue))
                            {
                                continue;
                            }
                        }

                        var found = new HashSet<(int sequenceIndex, int foundStartMovementIndex)>();
                        var foundFirsts = new HashSet<(int sequenceIndex, int foundStartMovementIndex)>();
                        var foundFirstsFulls = new HashSet<(int sequenceIndex, int foundStartMovementIndex)>();
                        for (var sequenceIndex = 0; sequenceIndex < sequences.Count; sequenceIndex++) // look in every sequence
                        {
                            var lookInSequence = sequences[sequenceIndex];
                            var firstMovementFromIndex = lookInSequence.FirstMovementFromIndex;

                            // gather results
                            var searchResult = FindMatchingSubsequences(lookInSequence, searchPhrase, true);
                            found.AddRange(searchResult.found.Select(x => (sequenceIndex, x - firstMovementFromIndex)));
                            foundFirsts.AddRange(searchResult.foundFirsts.Select(x => (sequenceIndex, x - firstMovementFromIndex)));
                            foundFirstsFulls.AddRange(searchResult.foundFirstsFulls.Select(x => (sequenceIndex, x - firstMovementFromIndex)));
                        }

                        if (foundFirstsFulls.Count > 1) // if it exists more than once
                        {
                            var isCompound = Enumerable
                                .Range(start, endMovement - start + 1)
                                .Select(i => (sequences[r].Movements.Span[i].FromType, roots[r][i]))
                                .AnyDuplicates(out _);

                            var successions = foundFirstsFulls
                                .GroupBy(x => x.sequenceIndex)
                                .Sum(g => g
                                    .Select(x => x.foundStartMovementIndex)
                                    .OrderBy(x => x)
                                    .WithPrevious()
                                    .Count(p => p.current - p.previous == searchPhraseLength));

                            if (isCompound && successions == 0) // if it's compound and never repeats immediately, it is not needed
                            {
                                continue;
                            }

                            loops.Add(new()
                            {
                                SequenceIndex = r,
                                Start = start,
                                EndMovement = endMovement,
                                Occurrences = foundFirstsFulls.Count,
                                Successions = successions,
                                Coverage = found.Select(x => sequences[x.sequenceIndex].FirstMovementFromIndex + x.foundStartMovementIndex).ToHashSet(),
                                FoundFirsts = foundFirsts.Select(x => sequences[x.sequenceIndex].FirstMovementFromIndex + x.foundStartMovementIndex).ToHashSet(),
                                Progression = searchPhrase,
                                IsCompound = isCompound,
                            });

                            foreach (var (sequenceIndex, foundStartMovementIndex) in found)
                            {
                                participationsInLoopIds[sequenceIndex][foundStartMovementIndex].Add(loopId);
                            }

                            foreach (var (sequenceIndex, foundStartMovementIndex) in foundFirstsFulls) // mark each finding
                            {
                                startsOfLoopIds[sequenceIndex][foundStartMovementIndex].Add(loopId);
                                endsOfLoopIds[sequenceIndex][foundStartMovementIndex + searchPhrase.Length].Add(loopId);
                            }

                            beginsWithKnownId ??= (loopId, 0);
                            loopId++;
                        }
                        else
                        {
                            // a progression with this start and end doesn't exist more than once, a longer with the same start won't exist
                            break; // break loop over endMovement, go to next start
                        }
                    }
                }
            }
        }

        return loops;
    }
}