using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Index;
using HarmonyDB.Index.Analysis.Models.Structure;
using HarmonyDB.Index.Analysis.Tools;
using OneShelf.Common;
using System.Text;
using HarmonyDB.Common.Representations.OneShelf;

namespace HarmonyDB.Index.Analysis.Services;

public class ProgressionsVisualizer
{
    public const string AttributeSearch = "search";
    public const string AttributeSearchFirst = "search-first";

    public string GetLoopStatisticsTitle(StructureLink link) =>
        $"{link.Successions:N2}/{link.Occurrences:N2}, {link.Coverage:P0}%";

    public string GetLoopChordsTitle(StructureLink link) =>
        link.Normalized.GetTitle(link.NormalizationRoot, true);

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


    public string VisualizeBlocks(ReadOnlyMemory<CompactHarmonyMovement> sequence, IReadOnlyList<byte> roots, IReadOnlyList<IBlock> blocks, bool typesToo = true)
    {
        var rootsTrace = CreateRootsTraceByIndices(sequence, roots, 0, sequence.Length - 1, out var positions, typesToo);

        var lines = blocks
            .Select((block, i) =>
            {
                var startPosition = positions[block.StartIndex];
                var endPosition = positions[block.EndIndex + 1];
                var specialPositions = block is LoopBlock loop
                    ? Enumerable.Range(0, block.BlockLength / loop.LoopLength + 1)
                        .Select(x => x * loop.LoopLength + block.StartIndex)
                        .Select(x => positions[x])
                    : Enumerable.Empty<int>();

                return string.Join(
                           string.Empty,
                           Enumerable
                               .Range(0, rootsTrace.Length)
                               .Select(j =>
                                   startPosition <= j &&
                                   endPosition >= j
                                       ? specialPositions.Contains(j)
                                           ? '|'
                                           : '-'
                                       : ' '))
                       + $"  {blocks[i].GetType().Name}";
            })
            .ToList();

        var result = string.Join(Environment.NewLine, string.Empty.Once()
            .Append(rootsTrace)
            .Concat(lines)
            .Append(string.Empty));
        return result;
    }

    public string CreateRootsTraceByIndices(
        ReadOnlyMemory<CompactHarmonyMovement> sequence,
        IReadOnlyList<byte> roots,
        int startIndex,
        int endIndex,
        out List<int> positions,
        bool typesToo = true)
    {
        var builder = new StringBuilder();
        if (typesToo)
        {
            builder.Append(new Note(roots[startIndex]).Representation(new()));
            builder.Append(sequence.Span[startIndex].FromType.ChordTypeToString());
        }
        else
        {
            builder.Append(roots[startIndex]);
        }

        positions =
        [
            0,
        ];

        for (var i = startIndex; i <= endIndex; i++)
        {
            builder.Append(' ');

            var startPosition = builder.Length;
            if (typesToo)
            {
                builder.Append(new Note(roots[i + 1]).Representation(new()));
                builder.Append(sequence.Span[i].ToType.ChordTypeToString());
            }
            else
            {
                builder.Append(roots[i + 1]);
            }

            positions.Add(startPosition);
        }

        return builder.ToString();
    }
}