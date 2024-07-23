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
using HarmonyDB.Index.Analysis.Models.Index.Graphs;
using HarmonyDB.Index.Analysis.Models.Index.Graphs.Interfaces;

namespace HarmonyDB.Index.Analysis.Services;

public class ProgressionsVisualizer(Dijkstra dijkstra, IndexExtractor indexExtractor)
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

    public Text VisualizeBlocksAsOne(ReadOnlyMemory<CompactHarmonyMovement> sequence, IReadOnlyList<byte> roots, IReadOnlyList<IBlock> blocks, BlockGraph graph, IReadOnlyList<IBlockJoint> shortestPath, BlocksChartParameters parameters)
    {
        var lines = VisualizeBlocks(sequence, roots, blocks, graph, shortestPath, parameters);
        return Text.Join(Text.NewLine, lines.Select(x => $"{x.left}   {x.right}".AsText()));
    }

    public (Text left, Text right) VisualizeBlocksAsTwo(ReadOnlyMemory<CompactHarmonyMovement> sequence, IReadOnlyList<byte> roots, IReadOnlyList<IBlock> blocks, BlockGraph graph, IReadOnlyList<IBlockJoint> shortestPath, BlocksChartParameters parameters)
    {
        var blockVisualizations = VisualizeBlocks(sequence, roots, blocks, graph, shortestPath, parameters);

        var left = Text.Join(Text.NewLine, blockVisualizations.Select(v => v.left));
        var right = Text.Join(Text.NewLine, blockVisualizations.Select(v => v.right));

        return (left, right);
    }

    private List<(Text left, Text right)> VisualizeBlocks(ReadOnlyMemory<CompactHarmonyMovement> sequence, IReadOnlyList<byte> roots, IReadOnlyList<IBlock> blocks, BlockGraph graph, IReadOnlyList<IBlockJoint> shortestPath, BlocksChartParameters parameters)
    {
        var rootsTrace = CreateRootsTraceByIndices(sequence, roots, out var positions, parameters.TypesToo);

        var gridPositions = positions.Where((_, i) => i % 6 == 5).ToList();

        var shortestPathBlocks = shortestPath.SelectSingle(x => x[0].Block1.Once().Concat(x.Select(x => x.Block2))).Select(x => x.Block).ToHashSet();

        var lines = new List<(Text left, Text right)>
        {
            (rootsTrace.AsText(), " ".AsText()) // a fix for linux
        };
        
        lines.AddRange(CreateBlocksLines(blocks, graph, parameters, positions, rootsTrace, shortestPathBlocks, gridPositions));
        
        var jointLines = CreateJointsLines(graph, positions, rootsTrace);

        if (jointLines.Any())
        {
            lines.Add((" ".AsText(), " ".AsText()));
            lines.AddRange(jointLines);
        }

        return lines;
    }

    private static List<(Text left, Text right)> CreateJointsLines(BlockGraph graph, List<int> positions, string rootsTrace)
    {
        var jointTitles = graph.Joints
            .Where(x => !x.IsEdge)
            .GroupBy(x => x.Normalization)
            .SelectMany((g, i) =>
                g.Select(j => (title: (char)('a' + i), positionIndex: positions[j.Block2.Block.StartIndex])))
            .GroupBy(x => x.positionIndex)
            .ToDictionary(x => x.Key, x => x.Select(x => x.title).OrderBy(x => x).ToList());

        return Enumerable
            .Range(0, jointTitles.Max(x => x.Value.Count))
            .Reverse()
            .Select(lineIndex => (
                new string(
                        Enumerable
                            .Range(0, rootsTrace.Length)
                            .Select(i => jointTitles.GetValueOrDefault(i)?.SelectSingle(t => lineIndex < t.Count ? t[lineIndex] : ' ') ?? ' ')
                            .ToArray())
                    .AsText(),
                lineIndex == 0 ? $"joints: {graph.Joints.Count}".AsText() : Text.Empty))
            .ToList();
    }

    private static List<(Text left, Text right)> CreateBlocksLines(IReadOnlyList<IBlock> blocks, BlockGraph graph, BlocksChartParameters parameters, List<int> positions, string rootsTrace, HashSet<IIndexedBlock> shortestPathBlocks, List<int> gridPositions)
        => blocks
            .Where(x => x is not EdgeBlock)
            .GroupBy(x => x switch
            {
                LoopBlock loopBlock when parameters.GroupNormalized => loopBlock.Normalized,
                SequenceBlock sequenceBlock when parameters.GroupNormalized => sequenceBlock.Normalized,
                PingPongBlock pingPongBlock when parameters.GroupNormalized => pingPongBlock.Normalized,
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

                return (left: Text.Join(
                        Text.Empty,
                        Enumerable
                            .Range(0, rootsTrace.Length)
                            .Select(j =>
                            {
                                var found = grouping.Where(b => positions[b.StartIndex] <= j && positions[b.EndIndex + 1] >= j).ToList();

                                if (found.Count > 2) throw new("Could not have happened.");

                                var uselessLoop = found.All(x => x is IIndexedBlock indexedBlock && graph.Environments[indexedBlock].Detections.HasFlag(BlockDetections.UselessLoop));
                                var shortestPathLoop = found.Any(x => x is IIndexedBlock && shortestPathBlocks.Contains(x) == true);
                                
                                var isModulation = found.FirstOrDefault() switch
                                {
                                    LoopBlock loopBlock => loopBlock.NormalizationRoot != grouping.Cast<LoopBlock>().First().NormalizationRoot,
                                    SequenceBlock sequenceBlock => sequenceBlock.NormalizationRoot != grouping.Cast<SequenceBlock>().First().NormalizationRoot,
                                    _ => false,
                                };

                                var css = uselessLoop
                                    ? Text.CssTextLightYellow
                                    : null;

                                return found.Any()
                                    ? (periodPositions.Contains(j)
                                        ? '|'
                                        : almostPeriodPositions.Contains(j)
                                            ? '\u25e6'
                                            : isModulation
                                                ? '~'
                                                : shortestPathLoop
                                                    ? '='
                                                    : '-').AsText(css)
                                    : gridPositions.Contains(j)
                                        ? '+'.AsText(Text.CssTextLightGray)
                                        : ' '.AsText();
                            })),
                    right: $"{(grouping.Count() == 1 || grouping.First() is PingPongBlock ? grouping.First().GetType().Name : $"{grouping.Key} \u00d7{grouping.Count()}")}".AsText());
            })
            .ToList();

    public string CreateRootsTraceByIndices(
        ReadOnlyMemory<CompactHarmonyMovement> sequence,
        IReadOnlyList<byte> roots,
        out List<int> positions,
        bool typesToo = true,
        IBlock? block = null)
    {
        var startIndex = block?.StartIndex ?? 0;
        var endIndex = block?.EndIndex ?? (sequence.Length - 1);
        
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