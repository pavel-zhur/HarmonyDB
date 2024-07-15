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
    public class TonalitiesController : Controller
    {
        private readonly ILogger<TonalitiesController> _logger;
        private readonly IndexApiClient _indexApiClient;

        public TonalitiesController(ILogger<TonalitiesController> logger, IndexApiClient indexApiClient)
        {
            _logger = logger;
            _indexApiClient = indexApiClient;
        }

        public IActionResult Index()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet]
        public async Task<IActionResult> StructureLoops(StructureLoopsModel model)
        {
            try
            {
                if (!model.JustForm)
                {
                    if (model.IncludeTrace)
                    {
                        ViewBag.Trace = new ApiTraceBag();
                    }

                    ViewBag.Response = await _indexApiClient.TonalitiesLoops(model, ViewBag.Trace);
                }

                model.JustForm = false;

                return View(model);
            }
            catch (ConcurrencyException)
            {
                return View("Concurrency");
            }
        }

        [HttpGet]
        public async Task<IActionResult> StructureSongs(StructureSongsModel model)
        {
            try
            {
                if (!model.JustForm)
                {
                    if (model.IncludeTrace)
                    {
                        ViewBag.Trace = new ApiTraceBag();
                    }

                    ViewBag.Response = await _indexApiClient.TonalitiesSongs(model, ViewBag.Trace);
                }

                model.JustForm = false;

                return View(model);
            }
            catch (ConcurrencyException)
            {
                return View("Concurrency");
            }
        }

        public async Task<IActionResult> StructureLoop(StructureLoopModel model)
        {
            try
            {
                if (model.IncludeTrace)
                {
                    ViewBag.Trace = new ApiTraceBag();
                }

                ViewBag.Response = await _indexApiClient.TonalitiesLoop(model, ViewBag.Trace);

                return View(model);
            }
            catch (ConcurrencyException)
            {
                return View("Concurrency");
            }
        }

        public async Task<IActionResult> StructureSong(StructureSongModel model)
        {
            try
            {
                if (model.IncludeTrace)
                {
                    ViewBag.Trace = new ApiTraceBag();
                }

                ViewBag.Response = await _indexApiClient.TonalitiesSong(model, ViewBag.Trace);
            
                return View(model);
            }
            catch (ConcurrencyException)
            {
                return View("Concurrency");
            }
        }
    }
}
