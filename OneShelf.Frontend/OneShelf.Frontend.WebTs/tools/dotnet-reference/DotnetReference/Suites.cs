using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyDB.Common;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Index.Blocks;
using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;
using HarmonyDB.Index.Analysis.Models.Index.Graphs;
using HarmonyDB.Index.Analysis.Models.Index;
using HarmonyDB.Index.Analysis.Models.Index.Graphs.Interfaces;
using HarmonyDB.Index.Analysis.Models.Structure;
using HarmonyDB.Index.Analysis.Models.TextGraphics;
using HarmonyDB.Index.Analysis.Models.V1;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.Analysis.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using OneShelf.Common;
using static DotnetReference.J;
using ISearchableChordsProgressionAlias = HarmonyDB.Index.Analysis.Models.Interfaces.ISearchableChordsProgression;

namespace DotnetReference;

public static partial class Suites
{
    // One instance of each service for the whole run, as the site's singletons.
    private static readonly ChordDataParser ChordDataParser = new(NullLogger<ChordDataParser>.Instance);
    private static readonly ProgressionsBuilder ProgressionsBuilder = new(NullLogger<ProgressionsBuilder>.Instance);
    private static readonly ProgressionsSearch ProgressionsSearch = new(NullLogger<ProgressionsSearch>.Instance);
    private static readonly ProgressionsVisualizer ProgressionsVisualizer = new();
    private static readonly PathsSearcher PathsSearcher = new();
    private static readonly IndexExtractor IndexExtractor = new(PathsSearcher);
    private static readonly InputParser InputParser = new(ProgressionsBuilder, ChordDataParser);

    public static JsonNode? Run(string suite, JsonElement input) => suite switch
    {
        "chord" => Chord(input),
        "note" => NoteSuite(input),
        "html" => Html(input),
        "search" => Search(input),
        "loop" => Loop(input),
        "links" => Links(input),
        "tonal" => Tonal(input),
        "fts" => Fts(input),
        "dotnet" => Dotnet(input),
        "json" => Json(input),
        "uri" => UriSuite(input),
        _ => throw new ArgumentOutOfRangeException(nameof(suite), suite, null),
    };

    // Projections.

    private static JsonNode? HarmonyData(HarmonyDataV1? x) => x == null ? null : A(I(x.Root), I((int?)x.Alteration), I((int)x.ChordType));

    private static JsonNode ChordData(ChordDataV1 x) => A(S(x.OriginalRepresentation), S(x.HarmonyRepresentation), HarmonyData(x.HarmonyData), S(x.ChordData));

    private static JsonNode Movements(ReadOnlyMemory<CompactHarmonyMovement> movements) => A(movements.ToArray().Select(m => (JsonNode?)A(I(m.RootDelta), I((int)m.FromType), I((int)m.ToType))));

    private static JsonNode Sequence(HarmonyMovementsSequence s, IReadOnlyList<HarmonyGroup> groups) => O(
        ("fr", I(s.FirstRoot)),
        ("ffi", Try(() => I(s.FirstMovementFromIndex))),
        ("m", A(s.Movements.Select(m => (JsonNode?)A(I(m.RootDelta), I((int)m.FromType), I((int)m.ToType), S(m.Title), I(IndexOfRef(groups, m.From)), I(IndexOfRef(groups, m.To)))))));

    private static JsonNode Progression(ChordsProgression p)
    {
        var groups = p.HarmonySequence.Select(x => x.harmonyGroup).ToList();
        var standard = p.StandardHarmonyMovementsSequences;
        var extended = p.ExtendedHarmonyMovementsSequences;
        JsonNode? Index(HarmonyMovementsSequence? s, IReadOnlyList<HarmonyMovementsSequence>? list) => s == null ? null : I(list == null ? -2 : IndexOfRef(list, s));

        return O(
            ("h", A(p.HarmonySequence.Select(x => (JsonNode?)A(
                I(x.harmonyGroup.Index),
                I(x.harmonyGroup.StartChordIndex),
                I(x.harmonyGroup.EndChordIndex),
                S(x.harmonyGroup.HarmonyRepresentation),
                HarmonyData(x.harmonyGroup.HarmonyData),
                B(x.harmonyGroup.IsStandard),
                A(x.harmonyGroup.OriginalSequence.Select(c => S(c.ChordData))),
                Index(x.standardMovementsSequence, standard),
                Index(x.extendedMovementsSequence, extended))))),
            ("o", A(p.OriginalSequence.Select(x => (JsonNode?)A(
                S(x.original.ChordData),
                I(IndexOfRef(groups, x.harmonyGroup)),
                I(x.indexInHarmonyGroup),
                Index(x.standardMovementsSequence, standard),
                Index(x.extendedMovementsSequence, extended))))),
            ("s", standard == null ? null : A(standard.Select(s => Sequence(s, groups)))),
            ("e", A(extended.Select(s => Sequence(s, groups)))));
    }

    private static JsonNode SearchResult(
        IReadOnlyList<ChordsProgression> progressions,
        (Dictionary<ChordsProgression, float> foundProgressionsWithCoverage, Dictionary<HarmonyGroup, bool> harmonyGroupsWithIsFirst) result)
        => O(
            ("f", A(result.foundProgressionsWithCoverage.Select(x => (JsonNode?)A(I(IndexOfRef(progressions, x.Key)), D(x.Value))))),
            ("g", A(result.harmonyGroupsWithIsFirst.Select(x =>
            {
                var p = progressions.Select((p, i) => (p, i)).First(p => p.p.HarmonySequence.Any(g => ReferenceEquals(g.harmonyGroup, x.Key)));
                return (JsonNode?)A(I(p.i), I(IndexOfRef(p.p.HarmonySequence.Select(g => g.harmonyGroup), x.Key)), B(x.Value));
            }))));

    private static JsonNode CustomAttributes(IReadOnlyDictionary<int, string> attributes) => A(attributes.Select(x => (JsonNode?)A(I(x.Key), S(x.Value))));

    private static JsonNode StructureLink(StructureLink link) => O(
        ("n", S(link.Normalized)),
        ("x", S(link.ExternalId)),
        ("r", I(link.NormalizationRoot)),
        ("o", D(link.Occurrences)),
        ("s", D(link.Successions)),
        ("c", D(link.Coverage)),
        ("st", Try(() => S(ProgressionsVisualizer.GetLoopStatisticsTitle(link)))),
        ("ct", Try(() => S(ProgressionsVisualizer.GetLoopChordsTitle(link)))));

    // Reading the input.

    public static RepresentationSettings Settings(JsonElement e)
    {
        Dictionary<int, string>? customAttributes = e.Has("ca")
            ? e.Arr("ca").ToDictionary(x => x[0].GetInt32(), x => x[1].GetString()!)
            : null;
        var settings = new RepresentationSettings(
            e.Int("t"),
            (NoteAlteration?)e.IntN("a"),
            e.Bool("x"),
            e.Bool("vw"),
            (SimplificationMode)e.Int("s"),
            customAttributes);
        if (e.Has("rel"))
        {
            var rel = e.GetProperty("rel");
            settings = settings with { RelativeTo = (rel[0].GetInt32(), rel[1].GetBoolean()) };
        }

        foreach (var op in e.Str("ops") ?? string.Empty)
        {
            settings = op == 'u' ? settings.Up() : settings.Down();
        }

        return settings;
    }

    private static ReadOnlyMemory<CompactHarmonyMovement> ReadMovements(JsonElement e) => e.EnumerateArray()
        .Select(m => new CompactHarmonyMovement
        {
            RootDelta = m[0].GetByte(),
            FromType = (ChordType)m[1].GetByte(),
            ToType = (ChordType)m[2].GetByte(),
        })
        .ToArray();

    private static List<string> Strings(this JsonElement e, string name) => e.Arr(name).Select(x => x.GetString()!).ToList();

    // Suites.

    private static JsonNode Chord(JsonElement input)
    {
        var s = input.Str("s")!;
        return O(
            ("get", Try(() => ChordDataParser.GetChord(s) is { } x
                ? O(
                    ("chord", A(x.chord.Select(n => (JsonNode?)I(n)))),
                    ("root", I(x.root)),
                    ("bass", I(x.bass)),
                    ("cf", I((int)x.chordTypeForCircleOfFifths)),
                    ("cp", I((int)x.chordTypeForProgressions)),
                    ("fing", S(x.fingering)),
                    ("ra", I((int?)x.rootAlteration)),
                    ("ba", I((int?)x.bassAlteration)))
                : null)),
            ("notes", Try(() => ChordDataParser.GetNotes(s) is { } x
                ? O(
                    ("bass", A(x.bass.Select(n => (JsonNode?)I(n)))),
                    ("main", A(x.main.Select(n => (JsonNode?)I(n)))),
                    ("root", I(x.root)),
                    ("fing", S(x.fingering)))
                : null)),
            ("data", Try(() => ChordData(ChordDataParser.GetProgressionData(s)))),
            ("input", Try(() => ChordData(InputParser.ParseChord(s)))));
    }

    private static JsonNode NoteSuite(JsonElement input)
    {
        var note = new Note((byte)input.Int("v"), (NoteAlteration?)input.IntN("a"));
        var settings = Settings(input.GetProperty("st"));
        var delta = input.Int("d");
        JsonNode NoteOut(Note n) => A(I(n.Value), I((int?)n.Alteration));
        return O(
            ("rep", Try(() => S(note.Representation(settings)))),
            ("tone", Try(() => I(note.Tone(settings)))),
            ("len", I(note.Length())),
            ("elen", Try(() => I(note.EstimatedLength(settings)))),
            ("sharp", Try(() => NoteOut(note.Sharp()))),
            ("flat", Try(() => NoteOut(note.Flat()))),
            ("add", Try(() => NoteOut(note.Add(delta)))),
            ("norm", I(Note.Normalize(delta))),
            ("settings", O(("t", I(settings.Transpose)), ("a", I((int?)settings.Alteration)), ("s", I((int)settings.Simplification)))),
            ("cof", A(Note.CircleOfFifths.Select(x => (JsonNode?)A(NoteOut(x.major), NoteOut(x.minor), B(x.isSharps))))));
    }

    private static JsonNode Html(JsonElement input)
    {
        var json = input.Str("json")!;
        NodeHtml Read() => JsonSerializer.Deserialize<NodeHtml>(json)!;
        var settingsList = input.Arr("st").Select(Settings).ToList();
        var result = O(
            ("lyrics", Try(() => S(Read().AsLyrics()))),
            ("lyricsRaw", Try(() => S(Read().AsLyrics(false)))),
            ("counts", Try(() => A(I(Read().GetChordsCount()), I(Read().GetNodesCount())))),
            ("compress", Try(() =>
            {
                var html = Read();
                html.Fix0_Compress();
                return S(JsonSerializer.Serialize(html));
            })),
            ("per", A(settingsList.Select(settings => (JsonNode?)O(
                ("html", Try(() => S(Read().AsHtml(settings)))),
                ("chords", Try(() => A(Read().AsChords(settings).Select(S)))),
                ("oneShelf", Try(() => S(Read().AsOneShelf(settings)))),
                ("all", Try(() => A(Read().GetAllChords(settings).Select(c => S(c.Representation(settings)))))))))));

        if (input.Bool("pipeline"))
        {
            var settings = settingsList[0];
            result["pipeline"] = Try(() => Pipeline(Read(), settings, input.Arr("ex").Select(x => (x[0].GetInt32(), x[1].GetInt32())).ToList()));
        }

        return result;
    }

    // What the site does with a song's chords (ChordsView, ProgressionsCacheLoader, DataProvider).
    private static JsonNode Pipeline(NodeHtml html, RepresentationSettings settings, List<(int start, int end)> extracts)
    {
        var result = new JsonObject();
        var chords = html.AsChords(settings);
        var data = chords.Select(ChordDataParser.GetProgressionData).ToList();
        result["data"] = A(data.Select(ChordData));
        var progression = ProgressionsBuilder.BuildProgression(data);
        result["progression"] = Progression(progression);
        result["progressionStd"] = Try(() => Progression(ProgressionsBuilder.BuildProgression(data, true)));
        result["compressed"] = Try(() =>
        {
            var compressed = CompressedChordsProgressionDataV1.Compress(data);
            return O(
                ("seq", A(compressed.Sequence.Select(x => (JsonNode?)I(x)))),
                ("distinct", A(compressed.Distinct.Select(ChordData))),
                ("same", B(compressed.Decompress().SequenceEqual(data))));
        });
        var compact = progression.Compact();
        result["compact"] = Try(() => S(Convert.ToBase64String(compact.Serialize().ToArray())));
        result["compactBack"] = Try(() => S(Convert.ToBase64String(CompactChordsProgression.Deserialize(compact.Serialize()).Serialize().ToArray())));

        var links = IndexExtractor.FindStructureLinks("does not matter", compact);
        result["links"] = A(links.Select(link => (JsonNode?)O(
            ("link", StructureLink(link)),
            ("search", Try(() =>
            {
                var searchProgression = InputParser.ParseSequence(link.Normalized.GetTitle(loopify: true));
                var found = ProgressionsSearch.Search(progression.Once().ToList(), searchProgression);
                var attributes = ProgressionsVisualizer.BuildCustomAttributesForSearch(progression, found);
                return O(
                    ("phrase", Movements(searchProgression.Compact().Movements)),
                    ("found", SearchResult([progression], found)),
                    ("attributes", CustomAttributes(attributes)),
                    ("html", Try(() => S(html.AsHtml(settings with { CustomAttributes = attributes })))));
            })))));
        result["sortedLinks"] = Try(() => A(links.WithIndices().OrderBy(x => x.x.Normalized.DeserializeLoop().Length).Select(x => (JsonNode?)I(x.i))));

        result["extracts"] = A(extracts.Select(x => Try(() =>
        {
            var search = ProgressionsSearch.ExtractSearch(progression, x.start, x.end);
            if (search == null) return null;
            var groups = progression.HarmonySequence.Select(g => g.harmonyGroup).ToList();
            var found = ProgressionsSearch.Search(progression.Once().ToList(), search);
            var attributes = ProgressionsVisualizer.BuildCustomAttributesForSearch(progression, found);
            return O(
                ("search", Sequence(search, groups)),
                ("found", SearchResult([progression], found)),
                ("attributes", CustomAttributes(attributes)),
                ("html", Try(() => S(html.AsHtml(new RepresentationSettings(settings.Transpose, settings.Alteration, settings.TestOnlyX, settings.IsVariableWidth, settings.Simplification, attributes))))));
        })));

        return result;
    }

    private static JsonNode Search(JsonElement input)
    {
        var progressions = input.Arr("songs")
            .Select(song => ProgressionsBuilder.BuildProgression(song.EnumerateArray().Select(c => ChordDataParser.GetProgressionData(c.GetString()!)).ToList()))
            .ToList();
        if (input.Bool("dup") && progressions.Count > 0) progressions.Add(progressions[0]);
        var compacts = progressions.Select(p => p.Compact()).ToList();
        var top = input.IntN("top");

        return O(
            ("progressions", A(progressions.Select(Progression))),
            ("results", A(input.Arr("ex").Select(x => Try(() =>
            {
                var progression = progressions[x[0].GetInt32()];
                var search = ProgressionsSearch.ExtractSearch(progression, x[1].GetInt32(), x[2].GetInt32());
                if (search == null) return null;
                var groups = progression.HarmonySequence.Select(g => g.harmonyGroup).ToList();
                return O(
                    ("search", Sequence(search, groups)),
                    ("found", Try(() => SearchResult(progressions, ProgressionsSearch.Search(progressions, search)))),
                    ("compact", Try(() => A(ProgressionsSearch.Search(compacts, search, top).Select(f => (JsonNode?)A(I(IndexOfRef(compacts, (CompactChordsProgression)f.Key)), D(f.Value)))))));
            })))));
    }

    private static JsonNode Block(IBlock b, IReadOnlyList<IBlock> all)
    {
        var result = O(
            ("k", S(b.GetType().Name)),
            ("t", I((int)b.Type)),
            ("s", I(b.StartIndex)),
            ("e", I(b.EndIndex)),
            ("l", I(b.BlockLength)));
        if (b is IIndexedBlock indexed)
        {
            result["n"] = S(indexed.Normalized);
            result["nr"] = I(indexed.NormalizationRoot);
            result["ch"] = A(indexed.Children.Select(c => (JsonNode?)A(I(IndexOfRef(all, c)), I((int)c.Type), I(c.StartIndex), I(c.EndIndex))));
            result["co"] = A(Enumerable.Range(b.StartIndex - 1, Math.Min(b.BlockLength, 40) + 2).Select(i => Try(() => S(indexed.GetNormalizedCoordinate(i)))));
        }

        if (b is LoopBlockBase loop)
        {
            result["loop"] = S(loop.Loop.SerializeLoop());
            result["sh"] = I(loop.NormalizationShift);
            result["occ"] = D(loop.Occurrences);
            result["suc"] = D(loop.Successions);
        }

        return result;
    }

    private static JsonNode Loop(JsonElement input)
    {
        var movements = ReadMovements(input.GetProperty("m"));
        var firstRoot = (byte)input.Int("fr");
        var result = new JsonObject
        {
            ["serialized"] = Try(() => S(movements.SerializeLoop())),
            ["normalized"] = Try(() =>
            {
                var normalized = IndexExtractor.GetNormalizedProgression(movements, out var shift, out var invariants);
                return A(S(normalized.SerializeLoop()), I(shift), I(invariants));
            }),
        };

        List<byte> roots;
        try
        {
            roots = IndexExtractor.CreateRoots(movements, firstRoot);
            result["roots"] = A(roots.Select(r => (JsonNode?)I(r)));
        }
        catch (Exception e)
        {
            result["roots"] = Error(e);
            return result;
        }

        result["loops"] = Try(() =>
        {
            var loops = IndexExtractor.FindLoops(movements, roots);
            return A(loops.Select(l => Block(l, loops)));
        });

        foreach (var types in input.Arr("types"))
        {
            var blockTypes = types.EnumerateArray().Select(t => (BlockType)t.GetInt32()).ToList();
            var key = "blocks" + string.Join("_", blockTypes.Select(t => (int)t));
            result[key] = Try(() =>
            {
                var blocks = IndexExtractor.FindBlocks(movements, roots, blockTypes, input.Bool("pp") ? new PolyBlocksExtractionParameters
                {
                    SequencesExcludeIfContainsAllChordsMoreThanOnce = true,
                    SequencesExcludeIfContainsSuccessiveLoopsBreak = true,
                    LoopsExcludeIfContainsAllChordsMoreThanOnce = true,
                    LoopsExcludeIfContainsSuccessiveLoopsBreak = true,
                } : null);
                var output = new JsonObject { ["blocks"] = A(blocks.Select(b => Block(b, blocks))) };
                BlockGraph graph;
                try
                {
                    graph = IndexExtractor.CreateGraph(blocks);
                }
                catch (Exception e)
                {
                    output["graph"] = Error(e);
                    return output;
                }

                output["joints"] = A(graph.Joints.Select(j => (JsonNode?)A(
                    I(IndexOfRef(blocks, j.Block1.Block)),
                    I(IndexOfRef(blocks, j.Block2.Block)),
                    Try(() => I(j.OverlapLength)),
                    Try(() => S(j.Normalization)),
                    B(j.IsEdge))));
                output["envs"] = A(graph.Environments.Select(x => (JsonNode?)A(
                    I(IndexOfRef(blocks, x.Key)),
                    A(x.Value.Children.Select(c => (JsonNode?)I(IndexOfRef(blocks, c.Block)))),
                    A(x.Value.ChildrenSubtree.Select(c => (JsonNode?)I(IndexOfRef(blocks, c.Block)))),
                    A(x.Value.Parents.Select(c => (JsonNode?)I(IndexOfRef(blocks, c.Block)))))));
                List<IBlockJoint> path;
                try
                {
                    path = PathsSearcher.Dijkstra(graph);
                    output["path"] = A(path.Select(j => (JsonNode?)I(IndexOfRef(graph.Joints, j))));
                }
                catch (Exception e)
                {
                    output["path"] = Error(e);
                    path = [];
                }

                foreach (var (name, parameters) in new[]
                         {
                             ("v1", new BlocksChartParameters()),
                             ("v2", new BlocksChartParameters { TypesToo = false, GroupNormalized = false, ShowJoints = false, ShowPath = false }),
                             ("v3", new BlocksChartParameters { GroupNormalized = false, ShowPath = false }),
                         })
                {
                    output[name] = Try(() =>
                    {
                        var one = ProgressionsVisualizer.VisualizeBlocksAsOne(movements, roots, blocks, graph, path, parameters);
                        var oneText = one.ToString();
                        var (left, right) = ProgressionsVisualizer.VisualizeBlocksAsTwo(movements, roots, blocks, graph, path, parameters);
                        var leftText = left.ToString();
                        return A(S(oneText), S(one.AsHtml()), S(leftText), S(right.ToString()), S(left.AsHtml()));
                    });
                }

                return output;
            });
        }

        return result;
    }

    private static JsonNode Links(JsonElement input)
    {
        var progression = new CompactChordsProgression
        {
            HarmonySequenceLength = input.Int("len"),
            ExtendedHarmonyMovementsSequences = input.Arr("seqs").Select(s => new CompactHarmonyMovementsSequence
            {
                FirstRoot = (byte)s.Int("fr"),
                FirstMovementFromIndex = s.Int("fi"),
                Movements = ReadMovements(s.GetProperty("m")),
            }).ToList(),
        };

        return O(
            ("serialized", Try(() => S(Convert.ToBase64String(progression.Serialize().ToArray())))),
            ("links", Try(() => A(IndexExtractor.FindStructureLinks(input.Str("id") ?? "x", progression).Select(StructureLink)))),
            ("compactSearch", Try(() =>
            {
                var phrase = InputParser.ParseSequence(input.Str("phrase")!);
                return A(ProgressionsSearch.Search(new List<ISearchableChordsProgressionAlias> { progression }, phrase, input.IntN("top")).Select(f => (JsonNode?)D(f.Value)));
            })));
    }
}
