using System.Globalization;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Structure;
using HarmonyDB.Index.Analysis.Tools;
using OneShelf.Common;
using System.Text;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models.Index.Blocks;
using HarmonyDB.Index.Analysis.Models.TextGraphics;
using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;

namespace HarmonyDB.Index.Analysis.Services;

public class ProgressionsVisualizer(ProgressionsOptimizer progressionsOptimizer, IndexExtractor indexExtractor)
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

    public Text VisualizeBlocksAsOne(ReadOnlyMemory<CompactHarmonyMovement> sequence, IReadOnlyList<byte> roots, IReadOnlyList<IBlock> blocks, BlocksChartParameters parameters)
    {
        var lines = VisualizeBlocks(sequence, roots, blocks, parameters);
        return Text.Join(Text.NewLine, lines.Select(x => $"{x.left}   {x.right}".AsText()));
    }

    public (Text left, Text right) VisualizeBlocksAsTwo(ReadOnlyMemory<CompactHarmonyMovement> sequence, IReadOnlyList<byte> roots, IReadOnlyList<IBlock> blocks, BlocksChartParameters parameters)
    {
        var blockVisualizations = VisualizeBlocks(sequence, roots, blocks, parameters);

        var left = Text.Join(Text.NewLine, blockVisualizations.Select(v => v.left));
        var right = Text.Join(Text.NewLine, blockVisualizations.Select(v => v.right));

        return (left, right);
    }
    
    public List<(Text left, Text right)> VisualizeBlocks(ReadOnlyMemory<CompactHarmonyMovement> sequence, IReadOnlyList<byte> roots, IReadOnlyList<IBlock> blocks, BlocksChartParameters parameters)
    {
        var graph = indexExtractor.FindGraph(blocks);
        var rootsTrace = CreateRootsTraceByIndices(sequence, roots, 0, sequence.Length - 1, out var positions, parameters.TypesToo);

        var gridPositions = positions.Where((_, i) => i % 6 == 5).ToList();

        var blocksToIds = new Dictionary<IBlock, int>();

        var blockId = 0;
        var lines = blocks
            .GroupBy(x => x switch
            {
                LoopBlock loopBlock when parameters.GroupNormalized => loopBlock.Normalized,
                SequenceBlock sequenceBlock when parameters.GroupNormalized => sequenceBlock.Normalized,
                _ => Random.Shared.NextDouble().ToString(CultureInfo.InvariantCulture),
            })
            .Select(grouping =>
            {
                blockId++;
                
                foreach (var block in grouping)
                {
                    blocksToIds[block] = blockId;
                }
                
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

                return (left: Text.Join(
                        Text.Empty,
                        Enumerable
                            .Range(0, rootsTrace.Length)
                            .Select(j =>
                            {
                                var found = grouping.Where(b => positions[b.StartIndex] <= j && positions[b.EndIndex + 1] >= j).ToList();

                                if (found.Count > 2) throw new("Could not have happened.");

                                var uselessLoop = found.All(x => x is IIndexedBlock indexedBlock && graph.EnvironmentsByBlock[indexedBlock].Detections.HasFlag(BlockDetections.UselessLoop));
                                
                                var isModulation = found.FirstOrDefault() switch
                                {
                                    LoopBlock loopBlock => loopBlock.NormalizationRoot != grouping.Cast<LoopBlock>().First().NormalizationRoot,
                                    SequenceBlock sequenceBlock => sequenceBlock.NormalizationRoot != grouping.Cast<SequenceBlock>().First().NormalizationRoot,
                                    _ => false,
                                };

                                var css = uselessLoop ? Text.CssTextLightYellow : null;
                                
                                return found.Any()
                                    ? periodPositions.Contains(j)
                                        ? '|'.AsText(css)
                                        : almostPeriodPositions.Contains(j)
                                            ? '\u25e6'.AsText(css)
                                            : isModulation
                                                ? '~'.AsText(css)
                                                : '-'.AsText(css)
                                    : gridPositions.Contains(j)
                                        ? '+'.AsText(Text.CssTextLightGray)
                                        : ' '.AsText();
                            })),
                    right: $"{blockId}: {(grouping.Count() == 1 ? grouping.Single().GetType().Name : $"{grouping.Key} \u00d7{grouping.Count()}")}".AsText());
            })
            .ToList();

        lines.Insert(0, (rootsTrace.AsText(), Text.Empty));

        lines.Add((Text.Empty, Text.Empty));
        var jointTitles = graph.Joints
            .GroupBy(x => x.Normalization)
            .SelectMany((g, i) =>
                g.Select(j => (title: (char)('a' + i), positionIndex: positions[j.Block2.Block.StartIndex])))
            .GroupBy(x => x.positionIndex)
            .ToDictionary(x => x.Key, x => x.Select(x => x.title).OrderBy(x => x).ToList());

        foreach (var lineIndex in Enumerable.Range(0, jointTitles.Max(x => x.Value.Count)).Reverse())
        {
            lines.Add((
                new string(Enumerable.Range(0, rootsTrace.Length).Select(i => jointTitles.GetValueOrDefault(i)?.SelectSingle(t => lineIndex < t.Count ? t[lineIndex] : ' ') ?? ' ').ToArray()).AsText(), 
                lineIndex == 0 ? $"joints: {graph.Joints.Count}".AsText() : Text.Empty));
        }
        
        if (parameters.AddPaths)
        {
            lines.Add((Text.Empty, Text.Empty));
            var paths = progressionsOptimizer.GetAllPossiblePaths(graph);
            var path = paths.MinBy(x => x.Count)!;
            lines.Add((
                string.Join(" ", path.Select(x => blocksToIds[x])).AsText(),
                $"1 / {paths.Count} ({paths.Min(x => x.Count)}..{paths.Max(x => x.Count)})".AsText()));
        }

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