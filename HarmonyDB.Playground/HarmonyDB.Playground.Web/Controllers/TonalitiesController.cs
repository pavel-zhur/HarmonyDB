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
using HarmonyDB.Playground.Web.Models.Tonalities;
using OneShelf.Common.Api.Common;

namespace HarmonyDB.Playground.Web.Controllers;

public class TonalitiesController : PlaygroundControllerBase
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
    public async Task<IActionResult> Loops(StructureLoopsModel model)
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
            return Concurrency();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Songs(StructureSongsModel model)
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
            return Concurrency();
        }
    }

    public async Task<IActionResult> Loop(StructureLoopModel model)
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
            return Concurrency();
        }
    }

    public async Task<IActionResult> Song(StructureSongModel model)
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
            return Concurrency();
        }
    }
}