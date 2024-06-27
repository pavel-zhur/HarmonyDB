using HarmonyDB.Index.Analysis.Models;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Services;

public class ProgressionsVisualizer
{
    private readonly ProgressionsSearch _progressionsSearch;

    public const string AttributeSearch = "search";
    public const string AttributeSearchFirst = "search-first";

    public ProgressionsVisualizer(ProgressionsSearch progressionsSearch)
    {
        _progressionsSearch = progressionsSearch;
    }

    public List<(Loop loop, int id, string title, string chordsTitle, bool isCompound, int length)> BuildLoopTitles(ChordsProgression progression)
    {
        var loops = _progressionsSearch.FindAllLoops(progression.Compact().ExtendedHarmonyMovementsSequences);
        var loopTitles = new List<(Loop loop, int id, string title, string chordsTitle, bool isCompound, int length)>();

        foreach (var (loop, id) in loops.WithIndices())
        {
            var length = loop.EndMovement - loop.Start + 1;
            var chordsTitle = string.Join(" ", Enumerable.Range(loop.Start, length).Append(loop.Start)
                .Select(i => progression.ExtendedHarmonyMovementsSequences[loop.SequenceIndex].FirstMovementFromIndex + i)
                .Select(i => progression.HarmonySequence[i].harmonyGroup.HarmonyRepresentation));
            var title = $"{loop.Successions}/{loop.Occurrences}, {loop.Coverage.Sum(h => progression.HarmonySequence[h].harmonyGroup.SelectSingle(x => x.EndChordIndex - x.StartChordIndex + 1)) * 100 / progression.OriginalSequence.Count}%";

            loopTitles.Add((loop, id, title, chordsTitle, loop.IsCompound, length));
        }

        return loopTitles;
    }

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

    public IReadOnlyDictionary<int, string> BuildCustomAttributesForLoop(IReadOnlyList<(Loop loop, int id, string title, string chordsTitle, bool isCompound, int length)> loops, ChordsProgression progression, int? loopId)
    {
        var loopsCustomAttributes = new Dictionary<int, string>();

        foreach (var x in loops.WithIndices()
                     .Where(x => x.i == loopId)
                     .SelectMany(l => l.x.loop.Coverage
                         .Select(i => progression.HarmonySequence[i].harmonyGroup)
                         .SelectMany(g => Enumerable.Range(g.StartChordIndex, g.EndChordIndex - g.StartChordIndex + 1))))
        {
            loopsCustomAttributes[x] = AttributeSearch;
        }

        foreach (var x in loops.WithIndices()
                     .Where(x => x.i == loopId)
                     .SelectMany(l => l.x.loop.FoundFirsts
                         .Select(i => progression.HarmonySequence[i].harmonyGroup)
                         .SelectMany(g => Enumerable.Range(g.StartChordIndex, g.EndChordIndex - g.StartChordIndex + 1))))
        {
            loopsCustomAttributes[x] = AttributeSearchFirst;
        }

        return loopsCustomAttributes;
    }
}