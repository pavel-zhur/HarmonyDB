using HarmonyDB.Index.Analysis.Models;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Services;

public class ProgressionsVisualizer
{
    public const string AttributeSearch = "search";
    public const string AttributeSearchFirst = "search-first";

    public string GetLoopStatisticsTitle(ChordsProgression progression, Loop loop) =>
        $"{loop.Successions}/{loop.Occurrences}, {loop.Coverage.Sum(h => progression.HarmonySequence[h].harmonyGroup.SelectSingle(x => x.EndChordIndex - x.StartChordIndex + 1)) * 100 / progression.OriginalSequence.Count}%";

    public string GetLoopChordsTitle(ChordsProgression progression, Loop loop) =>
        string.Join(" ", Enumerable.Range(loop.Start, loop.Length).Append(loop.Start)
            .Select(i => progression.ExtendedHarmonyMovementsSequences[loop.SequenceIndex].FirstMovementFromIndex + i)
            .Select(i => progression.HarmonySequence[i].harmonyGroup.HarmonyRepresentation));

    public IReadOnlyDictionary<int, string> BuildCustomAttributesForSearch(
        ChordsProgression progression,
        (Dictionary<ChordsProgression, float> foundProgressionsWithCoverage, Dictionary<HarmonyGroup, bool> harmonyGroupsWithIsFirst) searchResult)
        => progression.OriginalSequence
            .WithIndices()
            .Select(x => (
                x.x,
                x.i,
                isFirst: searchResult.harmonyGroupsWithIsFirst.TryGetValue(x.x.harmonyGroup, out var isFirst)
                    ? x.x.indexInHarmonyGroup == 0 && isFirst
                    : (bool?)null))
            .Where(x => x.isFirst.HasValue)
            .ToDictionary(x => x.i, x => x.isFirst!.Value ? AttributeSearchFirst : AttributeSearch);

    public IReadOnlyDictionary<int, string> BuildCustomAttributesForLoop(IReadOnlyList<Loop> loops, ChordsProgression progression, int? loopId)
    {
        var loopsCustomAttributes = new Dictionary<int, string>();

        foreach (var x in loops.WithIndices()
                     .Where(x => x.i == loopId)
                     .SelectMany(l => l.x.Coverage
                         .Select(i => progression.HarmonySequence[i].harmonyGroup)
                         .SelectMany(g => Enumerable.Range(g.StartChordIndex, g.EndChordIndex - g.StartChordIndex + 1))))
        {
            loopsCustomAttributes[x] = AttributeSearch;
        }

        foreach (var x in loops.WithIndices()
                     .Where(x => x.i == loopId)
                     .SelectMany(l => l.x.FoundFirsts
                         .Select(i => progression.HarmonySequence[i].harmonyGroup)
                         .SelectMany(g => Enumerable.Range(g.StartChordIndex, g.EndChordIndex - g.StartChordIndex + 1))))
        {
            loopsCustomAttributes[x] = AttributeSearchFirst;
        }

        return loopsCustomAttributes;
    }
}