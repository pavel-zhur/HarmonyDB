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
using Microsoft.AspNetCore.Localization;
using HarmonyDB.Index.Api.Model.VExternal1;

namespace HarmonyDB.Playground.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IndexApiClient _indexApiClient;
        private readonly SourceApiClient _sourceApiClient;
        private readonly ProgressionsSearch _progressionsSearch;
        private readonly ProgressionsBuilder _progressionsBuilder;
        private readonly ChordDataParser _chordDataParser;
        private readonly InputParser _inputParser;
        private readonly ProgressionsVisualizer _progressionsVisualizer;

        public HomeController(ILogger<HomeController> logger, IndexApiClient indexApiClient, SourceApiClient sourceApiClient, ProgressionsSearch progressionsSearch, ProgressionsBuilder progressionsBuilder, ChordDataParser chordDataParser, InputParser inputParser, ProgressionsVisualizer progressionsVisualizer)
        {
            _logger = logger;
            _indexApiClient = indexApiClient;
            _sourceApiClient = sourceApiClient;
            _progressionsSearch = progressionsSearch;
            _progressionsBuilder = progressionsBuilder;
            _chordDataParser = chordDataParser;
            _inputParser = inputParser;
            _progressionsVisualizer = progressionsVisualizer;
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
                ViewBag.Tonality = await _indexApiClient.StructureSong(new StructureSongRequest
                {
                    ExternalId = songModel.ExternalId,
                }, ViewBag.Trace);
            }
            catch (CacheItemNotFoundException e)
            {
                _logger.LogWarning(e, "The song tonality is not available.");
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

            if (songModel.DetectLoops)
            {
                var loops = _progressionsSearch.FindAllLoops(progression.Compact().ExtendedHarmonyMovementsSequences);
                if (songModel.LoopId.HasValue)
                {
                    var customAttributes = _progressionsVisualizer.BuildCustomAttributesForLoop(loops, progression, songModel.LoopId.Value);

                    representationSettings = representationSettings with { CustomAttributes = customAttributes };
                }

                ViewBag.Loops = loops;
                ViewBag.Progression = progression;
            }
            else if (songModel.Highlight != null)
            {
                var searchProgression = _inputParser.Parse(songModel.Highlight);
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
                return View("Concurrency");
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
                return View("Concurrency");
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

                    ViewBag.Response = await _indexApiClient.Loops(loopsModel, ViewBag.Trace);
                }

                loopsModel.JustForm = false;

                return View(loopsModel);
            }
            catch (ConcurrencyException)
            {
                return View("Concurrency");
            }
        }
    }
}
