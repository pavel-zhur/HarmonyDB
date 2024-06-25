using HarmonyDB.Playground.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Index.Api.Model.VExternal1;

namespace HarmonyDB.Playground.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IndexApiClient _indexApiClient;

        public HomeController(ILogger<HomeController> logger, IndexApiClient indexApiClient)
        {
            _logger = logger;
            _indexApiClient = indexApiClient;
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
        public async Task<IActionResult> Search([FromQuery]SearchRequest? request)
        {
            if (!string.IsNullOrWhiteSpace(request?.Query))
            {
                ViewBag.Response = await _indexApiClient.Search(request);
                ViewBag.Trace = new List
            }

            return View(request);
        }
    }
}
