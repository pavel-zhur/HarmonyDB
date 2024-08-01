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

public class ProgressionsVisualizer
{
    public const string AttributeSearch = "search";
    public const string AttributeSearchFirst = "search-first";
    private const char Bullet = '\u25e6';
    private const char Times = '\u00d7';

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
            (rootsTrace.AsText(), Text.EmptyLineContent)
        };
        
        lines.AddRange(CreateBlocksLines(blocks, graph, parameters, positions, rootsTrace.Length, shortestPathBlocks, gridPositions, out var blockGroups));

        if (parameters.ShowJoints)
        {
            var jointLines = CreateJointsLines(graph, positions, rootsTrace.Length);
            if (jointLines.Any())
            {
                lines.Add((Text.EmptyLineContent, Text.EmptyLineContent));
                lines.AddRange(jointLines);
            }
        }

        lines.Add((Text.EmptyLineContent, Text.EmptyLineContent));
        var pathLines = CreatePathLines(shortestPath, blockGroups);
        lines.AddRange(pathLines);

        return lines;
    }

    private List<(Text left, Text right)> CreatePathLines(IReadOnlyList<IBlockJoint> shortestPath, Dictionary<IIndexedBlock, int> blockGroups)
    {
        var titles = blockGroups
            .Select(x => (key: x.Key, title: x.Value.ToString()))
            .Append((key: shortestPath[0].Block1.Block, title: "ss"))
            .Append((key: shortestPath[^1].Block2.Block, title: "se"))
            .ToDictionary(x => x.key, x => x.title);

        var pathVisualization = shortestPath
            .GroupBy(x => (x.Block1.Block.Normalized, x.Block2.Block.Normalized))
            .Select(g =>
            {
                var path = $"{titles[g.First().Block1.Block]} -> {titles[g.First().Block2.Block]}".AsText();
                var counts = g.GroupBy(x => x.Normalization).Select(x => x.Count()).ToList();
                path.Append(counts is [1] 
                    ? Text.Empty
                    : counts.Count == 1
                        ? $" {Times}{counts[0]}".AsText()
                        : string.Join(",", counts.Select(x => $" {Times}{x}")).AsText(Text.CssTextRed));

                return path;
            })
            .ToList();

        return pathVisualization.Select(x => (x, EmptyLineContents: Text.EmptyLineContent)).ToList();
    }

    private static List<(Text left, Text right)> CreateJointsLines(BlockGraph graph, List<int> positions, int rootsTraceLength)
    {
        var jointTitles = graph.Joints
            .Where(x => !x.IsEdge)
            .GroupBy(x => x.Normalization)
            .SelectMany((g, i) =>
                g.Select(j => (title: (char)('a' + i), positionIndex: positions[j.Block2.Block.StartIndex])))
            .GroupBy(x => x.positionIndex)
            .ToDictionary(x => x.Key, x => x.Select(x => x.title).OrderBy(x => x).ToList());

        return Enumerable
            .Range(0, jointTitles.Max(x => (int?)x.Value.Count) ?? 0)
            .Reverse()
            .Select(lineIndex => (
                new string(
                        Enumerable
                            .Range(0, rootsTraceLength)
                            .Select(i => jointTitles.GetValueOrDefault(i)?.SelectSingle(t => lineIndex < t.Count ? t[lineIndex] : ' ') ?? ' ')
                            .ToArray())
                    .AsText(),
                lineIndex == 0 ? $"joints: {graph.Joints.Count}".AsText() : Text.Empty))
            .ToList();
    }

    private static List<(Text left, Text right)> CreateBlocksLines(
        IReadOnlyList<IBlock> blocks, 
        BlockGraph graph,
        BlocksChartParameters parameters, 
        List<int> positions, 
        int rootsTraceLength,
        HashSet<IIndexedBlock> shortestPathBlocks, 
        List<int> gridPositions,
        out Dictionary<IIndexedBlock, int> blockGroups)
    {
        var groups = blocks
            .Where(x => x is not EdgeBlock)
            .GroupBy(x => x switch
            {
                LoopBlockBase block when parameters.GroupNormalized => block.Type + block.Normalized,
                SequenceBlock sequenceBlock when parameters.GroupNormalized => "S" + sequenceBlock.Normalized,
                PingPongBlock pingPongBlock when parameters.GroupNormalized => "PP" + pingPongBlock.Normalized,
                RoundRobinBlock roundRobinBlock when parameters.GroupNormalized => "RR" + roundRobinBlock.Normalized,
                PolySequenceBlock polySequenceBlock when parameters.GroupNormalized => "PS" + polySequenceBlock.Normalized,
                _ => Random.Shared.NextDouble().ToString(CultureInfo.InvariantCulture),
            })
            .ToList();

        blockGroups = groups.WithIndices().SelectMany(g => g.x.OfType<IIndexedBlock>().Select(b => (b, g.i))).ToDictionary(x => x.b, x => x.i + 1);
        
        return groups
            .WithIndices()
            .SelectMany(pair =>
            {
                var (grouping, i) = pair;
                
                List<int> periodPositions = new(1), almostPeriodPositions = new(), polySequencePeriodPositions = new();
                foreach (var polySequenceBlock in grouping.OfType<PolySequenceBlock>())
                {
                    polySequencePeriodPositions.Add(positions[polySequenceBlock.StartIndex]);
                    polySequencePeriodPositions.Add(positions[polySequenceBlock.EndIndex + 1]);
                }

                foreach (var loop in grouping.OfType<LoopBlockBase>())
                {
                    periodPositions.AddRange(Enumerable.Range(0, loop.BlockLength / loop.LoopLength + 1)
                        .Select(x => x * loop.LoopLength + loop.StartIndex)
                        .Select(x => positions[x]));

                    if (loop.EachChordCoveredTimesWhole)
                    {
                        almostPeriodPositions.Add(positions[loop.EndIndex + 1]);
                    }
                }

                var left = Text.Join(
                    Text.Empty,
                    Enumerable
                        .Range(0, rootsTraceLength)
                        .Select(j =>
                        {
                            var found = grouping.Where(b => positions[b.StartIndex] <= j && positions[b.EndIndex + 1] >= j).ToList();

                            var uselessLoop = found.All(x => x is IIndexedBlock indexedBlock && graph.Environments[indexedBlock].Detections.HasFlag(BlockDetections.UselessLoop));
                            var shortestPathLoop = found.Any(shortestPathBlocks.Contains);

                            var isModulation = found.FirstOrDefault() switch
                            {
                                LoopBlockBase loopBlock => loopBlock.NormalizationRoot != grouping.Cast<LoopBlockBase>().First().NormalizationRoot,
                                RoundRobinBlock roundRobinBlock => roundRobinBlock.NormalizationRoot != grouping.Cast<RoundRobinBlock>().First().NormalizationRoot,
                                SequenceBlock sequenceBlock => sequenceBlock.NormalizationRoot != grouping.Cast<SequenceBlock>().First().NormalizationRoot,
                                PolySequenceBlock polySequenceBlock => polySequenceBlock.NormalizationRoot != grouping.Cast<PolySequenceBlock>().First().NormalizationRoot,
                                _ => false,
                            };
                                
                            var selfOverlapsDetected = grouping.OfType<IPolyBlock>().Any(x => x.SelfOverlapsDetected);

                            var css = uselessLoop
                                ? Text.CssTextLightYellow
                                : selfOverlapsDetected
                                    ? Text.CssTextRed
                                    : null;

                            if (!found.Any())
                                return gridPositions.Contains(j)
                                    ? '+'.AsText(Text.CssTextLightGray)
                                    : ' '.AsText();

                            char? @char = null;
                            if (found.Count > 1)
                            {
                                var starts = found.Any(x => positions[x.StartIndex] == j);
                                var ends = found.Any(x => positions[x.EndIndex + 1] == j);
                                var middles = found.Any(x => positions[x.StartIndex] != j && positions[x.EndIndex + 1] != j);
                                @char = starts && ends
                                    ? 'X'
                                    : starts && middles
                                        ? '<'
                                        : ends && middles
                                            ? '>'
                                            : null;
                            }

                            return (@char
                                    ?? (periodPositions.Contains(j)
                                        ? '|'
                                        : polySequencePeriodPositions.Contains(j)
                                            ? '+'
                                            : almostPeriodPositions.Contains(j)
                                                ? Bullet
                                                : isModulation
                                                    ? '~'
                                                    : shortestPathLoop
                                                        ? '='
                                                        : '-')).AsText(css);
                        }));

                var selfOverlap = grouping.OfType<IPolyBlock>().Any(x => x.SelfOverlapsDetected) ? "SOLP " : null;
                var roundRobinLevel = grouping.OfType<RoundRobinBlock>().Select(x => x.Level).FirstOrDefault().SelectSingle(x => x > 0 ? $"L{x} " : null);
                var title = grouping.Count() == 1 
                    ? grouping.First().GetType().Name 
                    : $"{(grouping.First() is PingPongBlock or PolySequenceBlock or PolyLoopBlock or RoundRobinBlock ? grouping.First().GetType().Name : grouping.Key)} {Times}{grouping.Count()}";
                
                var right = $"{i + 1}: {selfOverlap}{roundRobinLevel}{title}".AsText();
                
                return (left, right).Once();
            })
            .ToList();
    }

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