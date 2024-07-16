using HarmonyDB.Playground.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Globalization;
using HarmonyDB.Index.Analysis.Models.V1;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Source.Api.Client;
using HarmonyDB.Source.Api.Model.V1;
using HarmonyDB.Source.Api.Model.V1.Api;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Common.Api.Client;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using Microsoft.AspNetCore.Localization;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.Api.Model.VExternal1.Tonalities;
using HarmonyDB.Playground.Web.Models.Home;
using OneShelf.Common.Api.Common;

namespace HarmonyDB.Playground.Web.Controllers;

public class HomeController : PlaygroundControllerBase
{
    private readonly ILogger<HomeController> _logger;
    private readonly IndexApiClient _indexApiClient;
    private readonly SourceApiClient _sourceApiClient;
    private readonly ProgressionsSearch _progressionsSearch;
    private readonly IndexExtractor _indexExtractor;
    private readonly ProgressionsBuilder _progressionsBuilder;
    private readonly ChordDataParser _chordDataParser;
    private readonly InputParser _inputParser;
    private readonly ProgressionsVisualizer _progressionsVisualizer;

    public HomeController(ILogger<HomeController> logger, IndexApiClient indexApiClient, SourceApiClient sourceApiClient, ProgressionsSearch progressionsSearch, ProgressionsBuilder progressionsBuilder, ChordDataParser chordDataParser, InputParser inputParser, ProgressionsVisualizer progressionsVisualizer, IndexExtractor indexExtractor)
    {
        _logger = logger;
        _indexApiClient = indexApiClient;
        _sourceApiClient = sourceApiClient;
        _progressionsSearch = progressionsSearch;
        _progressionsBuilder = progressionsBuilder;
        _chordDataParser = chordDataParser;
        _inputParser = inputParser;
        _progressionsVisualizer = progressionsVisualizer;
        _indexExtractor = indexExtractor;
    }

    public IActionResult MyIp()
    {
        return Content(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown.");
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult SetCulture(string culture, string returnUrl)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new(CultureInfo.CurrentCulture.Name, culture)),
            new() { Expires = DateTimeOffset.UtcNow.AddYears(30) }
        );

        return LocalRedirect(returnUrl);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpGet]
    public async Task<IActionResult> Song(SongModel songModel)
    {
        if (songModel.IncludeTrace)
        {
            ViewBag.Trace = new ApiTraceBag();
        }

        Chords chords = (await _sourceApiClient.V1GetSong(_sourceApiClient.GetServiceIdentity(), songModel.ExternalId, ViewBag.Trace)).Song;
        ViewBag.Chords = chords;

        try
        {
            ViewBag.Tonality = await _indexApiClient.TonalitiesSong(new SongRequest
            {
                ExternalId = songModel.ExternalId,
            }, ViewBag.Trace);
        }
        catch (CacheItemNotFoundException e)
        {
            _logger.LogWarning(e, "The song tonality is not available.");
        }
        catch (ConcurrencyException)
        {
            ViewBag.TonalityConcurrencyException = true;
        }

        var representationSettings = new RepresentationSettings(
            transpose: songModel.Transpose, 
            alteration: songModel.Alteration);

        var chordsData = chords.Output.AsChords(representationSettings);
        var progression = _progressionsBuilder.BuildProgression(chordsData.Select(_chordDataParser.GetProgressionData).ToList());

        representationSettings = representationSettings with
        {
            IsVariableWidth = chords.Output.IsVariableWidth,
            Simplification = 
            (songModel.Show7 ? SimplificationMode.None : SimplificationMode.Remove7)
            | (songModel.Show9 ? SimplificationMode.None : SimplificationMode.Remove9AndMore)
            | (songModel.Show6 ? SimplificationMode.None : SimplificationMode.Remove6)
            | (songModel.ShowBass ? SimplificationMode.None : SimplificationMode.RemoveBass)
            | (songModel.ShowSus ? SimplificationMode.None : SimplificationMode.RemoveSus),
        };

        ViewBag.Parsed = chordsData
            .Distinct()
            .Select(x => (x, notes: _chordDataParser.GetNotes(x)))
            .Where(x => x.notes.HasValue)
            .ToDictionary(x => x.x, x => x.notes!.Value.SelectSingle(x => new PlayerModel
            {
                Bass = x.bass,
                Main = x.main,
            }));
            
        if (songModel.Highlight != null)
        {
            var searchProgression = _inputParser.ParseSequence(songModel.Highlight);
            var found = _progressionsSearch.Search(progression.Once().ToList(), searchProgression);

            var customAttributes = _progressionsVisualizer.BuildCustomAttributesForSearch(progression, found);

            representationSettings = representationSettings with { CustomAttributes = customAttributes };
        }

        ViewBag.RepresentationSettings = representationSettings;

        return View(songModel);
    }

    [HttpGet]
    public async Task<IActionResult> SongsByChords([FromQuery]SongsByChordsModel songsByChordsModel)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(songsByChordsModel.Query) && !songsByChordsModel.JustForm)
            {
                if (songsByChordsModel.IncludeTrace)
                {
                    ViewBag.Trace = new ApiTraceBag();
                }

                ViewBag.Response = await _indexApiClient.SongsByChords(songsByChordsModel, ViewBag.Trace);
            }

            songsByChordsModel.JustForm = false;
            if (string.IsNullOrEmpty(songsByChordsModel.Query)) songsByChordsModel = songsByChordsModel with { Query = "F G Am F" };

            return View(songsByChordsModel);
        }
        catch (ConcurrencyException)
        {
            return Concurrency();
        }
    }

    [HttpGet]
    public async Task<IActionResult> SongsByHeader([FromQuery]SongsByHeaderModel songsByHeaderModel)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(songsByHeaderModel.Query) && !songsByHeaderModel.JustForm)
            {
                if (songsByHeaderModel.IncludeTrace)
                {
                    ViewBag.Trace = new ApiTraceBag();
                }

                ViewBag.Response = await _indexApiClient.SongsByHeader(songsByHeaderModel, ViewBag.Trace);
            }

            songsByHeaderModel.JustForm = false;

            return View(songsByHeaderModel);
        }
        catch (ConcurrencyException)
        {
            return Concurrency();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Loops([FromQuery] LoopsModel loopsModel)
    {
        try
        {
            if (!loopsModel.JustForm)
            {
                if (loopsModel.IncludeTrace)
                {
                    ViewBag.Trace = new ApiTraceBag();
                }

                ViewBag.Response = await _indexApiClient.TonalitiesLoops(loopsModel, ViewBag.Trace);
            }

            loopsModel.JustForm = false;

            return View(loopsModel);
        }
        catch (ConcurrencyException)
        {
            return Concurrency();
        }
    }
}