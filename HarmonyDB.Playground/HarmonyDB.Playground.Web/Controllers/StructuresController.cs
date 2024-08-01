using HarmonyDB.Playground.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using HarmonyDB.Playground.Web.Models.Structures;
using HarmonyDB.Source.Api.Client;
using HarmonyDB.Source.Api.Model.V1.Api;
using OneShelf.Common.Api.Client;
using HarmonyDB.Index.Analysis.Services;

namespace HarmonyDB.Playground.Web.Controllers;

public class StructuresController : PlaygroundControllerBase
{
    private readonly ILogger<StructuresController> _logger;
    private readonly SourceApiClient _sourceApiClient;
    private readonly IndexExtractor _indexExtractor;
    private readonly ProgressionsBuilder _progressionsBuilder;
    private readonly ChordDataParser _chordDataParser;
    private readonly ProgressionsVisualizer _progressionsVisualizer;
    private readonly PathsSearcher _pathsSearcher;

    public StructuresController(ILogger<StructuresController> logger, SourceApiClient sourceApiClient, IndexExtractor indexExtractor, ProgressionsBuilder progressionsBuilder, ChordDataParser chordDataParser, ProgressionsVisualizer progressionsVisualizer, PathsSearcher pathsSearcher)
    {
        _logger = logger;
        _sourceApiClient = sourceApiClient;
        _indexExtractor = indexExtractor;
        _progressionsBuilder = progressionsBuilder;
        _chordDataParser = chordDataParser;
        _progressionsVisualizer = progressionsVisualizer;
        _pathsSearcher = pathsSearcher;
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpGet]
    public async Task<IActionResult> Multi(MultiModel model)
    {
        if (model.ToRemove.HasValue)
        {
            model.ExternalIds.RemoveAt(model.ToRemove.Value);
            model.ToRemove = null;
            return RedirectToAction("Multi", model);
        }

        model.ExternalIds = model.ExternalIds.Distinct().ToList();

        if (model.IncludeTrace)
        {
            ViewBag.Trace = new ApiTraceBag();
        }

        var songs = ((GetSongsResponse)await _sourceApiClient.V1GetSongs(_sourceApiClient.GetServiceIdentity(), model.ExternalIds, ViewBag.Trace)).Songs.Values.ToList();

        var stopwatch = new Stopwatch();
        TimeSpan
            compactTime = TimeSpan.Zero,
            rootsTime = TimeSpan.Zero,
            blocksTime = TimeSpan.Zero,
            graphTime = TimeSpan.Zero,
            dijkstraTime = TimeSpan.Zero,
            visualizationTime = TimeSpan.Zero;

        ViewBag.Visualizations = songs.Select(x =>
        {
            var chordsData = x.Output.AsChords(new());
            var progression = _progressionsBuilder.BuildProgression(chordsData.Select(_chordDataParser.GetProgressionData).ToList());

            return progression.ExtendedHarmonyMovementsSequences.Select(s =>
            {
                stopwatch.Restart();
                var compact = s.Compact();
                compactTime += stopwatch.Elapsed;

                stopwatch.Restart();
                var sequence = compact.Movements;
                var roots = _indexExtractor.CreateRoots(sequence, compact.FirstRoot);
                rootsTime += stopwatch.Elapsed;

                stopwatch.Restart();
                var blocks = _indexExtractor.FindBlocks(sequence, roots, model.BlockTypes, model);
                blocksTime += stopwatch.Elapsed;

                stopwatch.Restart();
                var graph = _indexExtractor.CreateGraph(blocks);
                graphTime += stopwatch.Elapsed;

                stopwatch.Restart();
                var shortestPath = _pathsSearcher.Dijkstra(graph);
                dijkstraTime += stopwatch.Elapsed;

                stopwatch.Restart();
                var visualization = _progressionsVisualizer.VisualizeBlocksAsTwo(sequence, roots, blocks, graph, shortestPath, new() { ShowJoints = false, ShowPath = false, });
                visualizationTime += stopwatch.Elapsed;

                return visualization;
            }).ToList();
        }).ToList();

        ViewBag.Songs = songs;
        ViewBag.Timers = new Dictionary<string, TimeSpan>
        {
            { nameof(compactTime), compactTime },
            { nameof(rootsTime), rootsTime },
            { nameof(blocksTime), blocksTime },
            { nameof(graphTime), graphTime },
            { nameof(dijkstraTime), dijkstraTime },
            { nameof(visualizationTime), visualizationTime }
        };

        return View(model);
    }
}