using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneShelf.Admin.Web.Authorization;
using OneShelf.Admin.Web.Models;
using OneShelf.Billing.Api.Client;
using OneShelf.Common.Database.Songs;
using OneShelf.Illustrations.Api.Client;
using SixLabors.ImageSharp.Formats.Png;

namespace OneShelf.Admin.Web.Controllers
{
    [MyAuthorizationFilter]
    public class HomeController : Controller
    {
        public const string AuthCookieName = "authv1";

        private readonly ILogger<HomeController> _logger;
        private readonly IllustrationsApiClient _illustrationsApiClient;
        private readonly BillingApiClient _billingApiClient;
        private readonly SongsDatabase _songsDatabase;
        private readonly AdminOptions _adminOptions;
        private readonly IHttpClientFactory _httpClientFactory;
        
        public HomeController(ILogger<HomeController> logger, IllustrationsApiClient illustrationsApiClient, BillingApiClient billingApiClient, IConfiguration configuration, SongsDatabase songsDatabase, IOptions<AdminOptions> adminOptions, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _illustrationsApiClient = illustrationsApiClient;
            _billingApiClient = billingApiClient;
            _songsDatabase = songsDatabase;
            _httpClientFactory = httpClientFactory;
            _adminOptions = adminOptions.Value;
        }

        [AllowAnonymous]
        [HttpPost]
        public IActionResult SetCookie(string cookie)
        {
            Response.Cookies.Append(AuthCookieName, cookie, new()
            {
                Expires = DateTimeOffset.Now.AddYears(10),
            });

            return RedirectToAction("Index");
        }

        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
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

        public async Task<IActionResult> Admin()
        {
            return View();
        }

        public async Task<IActionResult> ChordsTest()
        {
            return View();
        }
        
        [HttpGet]
        public async Task<IActionResult> Illustrations(int h = 96, IllustrationsModel.Mode mode = IllustrationsModel.Mode.None, int? justVersion = null)
        {
            var all = await _illustrationsApiClient.All();

            var hidden = all.Responses.Count;
            all.Responses = all.Responses.Where(x => x.Value.LatestCreatedOn < DateTime.Now.AddHours(-h))
                .ToDictionary(x => x.Key, x => x.Value);
            hidden -= all.Responses.Count;
            
            var urlsToTitles = (await _songsDatabase.Versions
                    .Where(x => x.Song.TenantId == _adminOptions.TenantId)
                    .Include(x => x.Song)
                    .ThenInclude(x => x.Artists)
                    .ToListAsync())
                .GroupBy(x => x.Uri)
                .ToDictionary(x => x.Key.ToString(), x =>
                {
                    var song = x.First().Song;
                    return $"{song.Index}. {string.Join(", ", song.Artists.Select(x => x.Name))} - {song.Title}";
                });

            return View(new IllustrationsModel(all, urlsToTitles, hidden, mode, all.Responses.Sum(x => x.Value.ImageIds.Sum(x => x.Value.Sum(x => x.Sum(x => x.Count)))), justVersion));
        }

        public async Task<IActionResult> Illustration(Guid id)
        {
            return File(await _illustrationsApiClient.GetImage(id), "image/jpeg");
        }

        public async Task<IActionResult> Billing(int? domainId)
        {
            var all = await _billingApiClient.All(domainId);

            for (var i = 0; i < all.Usages.Count; i++)
            {
                all.Usages[i] = all.Usages[i] with
                {
                    CreatedOn = all.Usages[i].CreatedOn!.Value.AddHours(3)
                };
            }

            return View(new BillingModel
            {
                All = all,
            });
        }

        public async Task<IActionResult> Png(PngModel pngModel)
        {
            using var httpClient = _httpClientFactory.CreateClient();
            await using var stream = await httpClient.GetStreamAsync(pngModel.Url1024);
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(stream);
            await using var pngStream = new MemoryStream();
            await image.SaveAsync(pngStream, new PngEncoder());
            return File(pngStream.ToArray(), "image/png", $"{Path.GetInvalidFileNameChars().Aggregate(pngModel.Title, (x, c) => x.Replace(c, '_'))}.png");
        }
    }
}