using System.Globalization;
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

    public string VisualizeBlocksAsOne(ReadOnlyMemory<CompactHarmonyMovement> sequence, IReadOnlyList<byte> roots, IReadOnlyList<IBlock> blocks, bool typesToo = true, bool groupNormalized = true)
    {
        var lines = VisualizeBlocks(sequence, roots, blocks, typesToo, groupNormalized);
        var result = string.Join(Environment.NewLine, lines.Select(x => $"{x.left}   {x.right}"));
        return result;
    }

    public (string left, string right) VisualizeBlocksAsTwo(ReadOnlyMemory<CompactHarmonyMovement> sequence, IReadOnlyList<byte> roots, IReadOnlyList<IBlock> blocks, bool typesToo = true, bool groupNormalized = true)
    {
        var blockVisualizations = VisualizeBlocks(sequence, roots, blocks, typesToo, groupNormalized);

        var left = string.Join(Environment.NewLine, blockVisualizations.Select(v => v.left));
        var right = string.Join(Environment.NewLine, blockVisualizations.Select(v => v.right));

        return (left, right);
    }
    
    public List<(string left, string right)> VisualizeBlocks(ReadOnlyMemory<CompactHarmonyMovement> sequence, IReadOnlyList<byte> roots, IReadOnlyList<IBlock> blocks, bool typesToo = true, bool groupNormalized = true)
    {
        var rootsTrace = CreateRootsTraceByIndices(sequence, roots, 0, sequence.Length - 1, out var positions, typesToo);

        var gridPositions = positions.Where((x, i) => i % 6 == 5).ToList();
        
        var lines = blocks
            .GroupBy(x => x switch
            {
                LoopBlock loopBlock when groupNormalized => loopBlock.Normalized,
                SequenceBlock sequenceBlock when groupNormalized => sequenceBlock.Normalized,
                _ => Random.Shared.NextDouble().ToString(CultureInfo.InvariantCulture),
            })
            .Select(grouping =>
            {
                List<int> periodPositions = new(), almostPeriodPositions = new();
                foreach (var loop in grouping.OfType<LoopBlock>())
                {
                    periodPositions.AddRange(Enumerable.Range(0, loop.BlockLength / loop.LoopLength + 1)
                            .Select(x => x * loop.LoopLength + loop.StartIndex)
                            .Select(x => positions[x]));

                    if (loop.EachChordCoveredTimesWhole)
                    {
                        almostPeriodPositions.Add(positions[loop.EndIndex + 1]);
                    }
                }

                return (left: string.Join(
                        string.Empty,
                        Enumerable
                            .Range(0, rootsTrace.Length)
                            .Select(j =>
                            {
                                var found = grouping.FirstOrDefault(b => positions[b.StartIndex] <= j && positions[b.EndIndex + 1] >= j);

                                var isModulation = found switch
                                {
                                    LoopBlock loopBlock => loopBlock.NormalizationRoot != grouping.Cast<LoopBlock>().First().NormalizationRoot,
                                    SequenceBlock sequenceBlock => sequenceBlock.NormalizationRoot != grouping.Cast<SequenceBlock>().First().NormalizationRoot,
                                    _ => false,
                                };

                                return found != null
                                    ? periodPositions.Contains(j)
                                        ? '|'
                                        : almostPeriodPositions.Contains(j)
                                            ? '\u25e6'
                                            : isModulation
                                                ? '~'
                                                : '-'
                                    : gridPositions.Contains(j)
                                        ? '+'
                                        : ' ';
                            })),
                    right: $"{(grouping.Count() == 1 ? grouping.Single().GetType().Name : $"{grouping.Key} \u00d7{grouping.Count()}")}");
            })
            .ToList();

        lines.Insert(0, (rootsTrace, string.Empty));
        return lines;
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