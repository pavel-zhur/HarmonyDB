using HarmonyDB.Playground.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Playground.Web.Models.Structures;
using HarmonyDB.Source.Api.Client;
using HarmonyDB.Playground.Web.Models.Home;
using HarmonyDB.Source.Api.Model.V1.Api;
using OneShelf.Common.Api.Client;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Source.Api.Model.V1;
using HarmonyDB.Index.Analysis.Models.Index.Blocks;

namespace HarmonyDB.Playground.Web.Controllers;

public class StructuresController : PlaygroundControllerBase
{
    private readonly ILogger<StructuresController> _logger;
    private readonly SourceApiClient _sourceApiClient;
    private readonly IndexExtractor _indexExtractor;
    private readonly ProgressionsBuilder _progressionsBuilder;
    private readonly ChordDataParser _chordDataParser;
    private readonly ProgressionsVisualizer _progressionsVisualizer;

    public StructuresController(ILogger<StructuresController> logger, SourceApiClient sourceApiClient, IndexExtractor indexExtractor, ProgressionsBuilder progressionsBuilder, ChordDataParser chordDataParser, ProgressionsVisualizer progressionsVisualizer)
    {
        _logger = logger;
        _sourceApiClient = sourceApiClient;
        _indexExtractor = indexExtractor;
        _progressionsBuilder = progressionsBuilder;
        _chordDataParser = chordDataParser;
        _progressionsVisualizer = progressionsVisualizer;
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
        
        ViewBag.Visualizations = songs.Select(x =>
        {
            var chordsData = x.Output.AsChords(new());
            var progression =
                _progressionsBuilder.BuildProgression(chordsData.Select(_chordDataParser.GetProgressionData).ToList());

            return progression.ExtendedHarmonyMovementsSequences.Select(s =>
            {
                var compact = s.Compact();
                var sequence = compact.Movements;
                var roots = _indexExtractor.CreateRoots(sequence, compact.FirstRoot);
                var blocks = _indexExtractor.FindBlocks(sequence, roots, BlocksExtractionLogic.Loops);
                return _progressionsVisualizer.VisualizeBlocksAsTwo(sequence, roots, blocks, new());
            }).ToList();
        }).ToList();
        
        ViewBag.Songs = songs;
        
        return View(model);
    }
}