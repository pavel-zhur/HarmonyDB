using System.Runtime.InteropServices;
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
        var (foundProgressionsWithCoverage, harmonyGroupsWithIsFirst) = Search<ISearchableChordsProgression, IReadOnlyList<ISearchableHarmonyMovementsSequence>, ISearchableHarmonyMovementsSequence>(
            progressions,
            p => (p.HarmonySequenceLength, p.ExtendedHarmonyMovementsSequences),
            searchPhrase,
            true);

        return (
            foundProgressionsWithCoverage.ToDictionary(x => (ChordsProgression)x.Key, x => x.Value),
            harmonyGroupsWithIsFirst.ToDictionary(x => ((ChordsProgression)x.Key.progression).HarmonySequence[x.Key.index].harmonyGroup, x => x.Value));
    }

    /// <summary>
    /// Finds the songs of the progressions, given by their positions there, where the search phrase is found.
    /// </summary>
    /// <returns>The positions of the songs found, with their coverage.</returns>
    public Dictionary<int, float> Search(CompactChordsProgressions progressions, IEnumerable<int> positions, HarmonyMovementsSequence searchPhrase, int? top = int.MaxValue)
        => Search<int, ArraySegment<CompactHarmonyMovementsSlice>, CompactHarmonyMovementsSlice>(
                positions,
                p => (progressions.GetHarmonySequenceLength(p), progressions.GetSequences(p)),
                searchPhrase,
                false,
                top)
            .foundProgressionsWithCoverage;

    private (Dictionary<TProgression, float> foundProgressionsWithCoverage, Dictionary<(TProgression progression, int index), bool> harmonyGroupsWithIsFirst) Search<TProgression, TSequences, TSequence>(
        IEnumerable<TProgression> progressions,
        Func<TProgression, (int harmonySequenceLength, TSequences extendedHarmonyMovementsSequences)> getProgression,
        HarmonyMovementsSequence searchPhrase,
        bool harmonyGroupsToo,
        int? top = int.MaxValue)
        where TProgression : notnull
        where TSequences : IReadOnlyList<TSequence>
        where TSequence : ISearchableHarmonyMovementsSequence
    {
        (Dictionary<TProgression, float> foundProgressionsWithCoverage, Dictionary<(TProgression progression, int index), bool> harmonyGroupsWithIsFirst) result = (new(), new());

        var isLoop = searchPhrase.Movements[^1].To.HarmonyRepresentation == searchPhrase.Movements[0].From.HarmonyRepresentation;
        var compactSearchPhrase = searchPhrase.Compact().Movements;

        foreach (var progression in progressions)
        {
            if (result.foundProgressionsWithCoverage.ContainsKey(progression)) continue;

            var (harmonySequenceLength, extendedHarmonyMovementsSequences) = getProgression(progression);

            HashSet<int>? totalFound = null, totalFoundFirsts = null;
            for (var i = 0; i < extendedHarmonyMovementsSequences.Count; i++)
            {
                var (found, foundFirsts, _) = FindMatchingSubsequences(extendedHarmonyMovementsSequences[i], compactSearchPhrase, isLoop);
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
                result.foundProgressionsWithCoverage.Add(progression, totalFound.Count / (float)harmonySequenceLength);

                if (harmonyGroupsToo)
                {
                    result.harmonyGroupsWithIsFirst.AddRange(totalFound.Select(i => ((progression, i), totalFoundFirsts!.Contains(i))), false);
                }

                if (result.foundProgressionsWithCoverage.Count == top)
                    break;
            }
        }
        
        return result;
    }

    private (HashSet<int> found, HashSet<int> foundFirsts, HashSet<int> foundFirstsFulls) FindMatchingSubsequences<TSequence>(TSequence sequence, ReadOnlyMemory<CompactHarmonyMovement> searchPhrase, bool isLoop)
        where TSequence : ISearchableHarmonyMovementsSequence
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
}