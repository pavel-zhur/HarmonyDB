using HarmonyDB.Playground.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Source.Api.Client;
using HarmonyDB.Source.Api.Model.V1;
using HarmonyDB.Source.Api.Model.V1.Api;
using Microsoft.Extensions.Options;
using OneShelf.Common.Api.Client;

namespace HarmonyDB.Playground.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IndexApiClient _indexApiClient;
        private readonly SourceApiClient _sourceApiClient;

        public HomeController(ILogger<HomeController> logger, IndexApiClient indexApiClient, SourceApiClient sourceApiClient)
        {
            _logger = logger;
            _indexApiClient = indexApiClient;
            _sourceApiClient = sourceApiClient;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
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

            ViewBag.Chords = (Chords)(await _sourceApiClient.V1GetSong(_sourceApiClient.GetServiceIdentity(), songModel.ExternalId, ViewBag.Trace)).Song;

            return View(songModel);
        }

        [HttpGet]
        public async Task<IActionResult> Search([FromQuery]SearchModel searchModel)
        {
            if (!string.IsNullOrWhiteSpace(searchModel.Query) && !searchModel.JustForm)
            {
                if (searchModel.IncludeTrace)
                {
                    ViewBag.Trace = new ApiTraceBag();
                }

                ViewBag.Response = await _indexApiClient.Search(searchModel, ViewBag.Trace);
            }

            searchModel.JustForm = false;

            return View(searchModel);
        }
    }
}
