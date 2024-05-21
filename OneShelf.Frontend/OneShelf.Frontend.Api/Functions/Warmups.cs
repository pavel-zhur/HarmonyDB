using HarmonyDB.Index.Api.Client;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Authorization.Api.Client;
using OneShelf.Common.Database.Songs;
using OneShelf.Frontend.Api.Model;

namespace OneShelf.Frontend.Api.Functions
{
    public class Warmups
    {
        private readonly IndexApiClient _indexApiClient;
        private readonly AuthorizationApiClient _authorizationApiClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly SongsDatabase _songsDatabase;
        private readonly FrontendOptions _options;
        private readonly ILogger _logger;

        public Warmups(ILoggerFactory loggerFactory, IndexApiClient indexApiClient, AuthorizationApiClient authorizationApiClient, IHttpClientFactory httpClientFactory, IOptions<FrontendOptions> options, SongsDatabase songsDatabase)
        {
            _indexApiClient = indexApiClient;
            _authorizationApiClient = authorizationApiClient;
            _httpClientFactory = httpClientFactory;
            _songsDatabase = songsDatabase;
            _options = options.Value;
            _logger = loggerFactory.CreateLogger<Warmups>();
        }

        [Function("Warmups")]
        public async Task Run([TimerTrigger("0 */4 * * * *", RunOnStartup = true)] MyInfo myTimer)
        {
            await SongsDatabase("run 1");
            await SongsDatabase("run 2");
            await SongsDatabase("run 3");

            await Run("run 1");
            await Run("run 2");
            await Run("run 3");
        }

        private async Task SongsDatabase(string title)
        {
            var started = DateTime.Now;
            await _songsDatabase.Users.CountAsync();
            _logger.LogInformation("{title}: songs database took {ms} ms.", title, (DateTime.Now - started).TotalMilliseconds);
        }

        private async Task Run(string title)
        {
            var authorization = DateTime.Now;
            await _authorizationApiClient.Ping();
            var authorizationTook = (int)(DateTime.Now - authorization).TotalMilliseconds;

            var index = DateTime.Now;
            await _indexApiClient.V1Ping();
            var indexTook = (int)(DateTime.Now - index).TotalMilliseconds;

            var telegram = DateTime.Now;
            int? telegramTook = null;
            if (_options.TelegramPingUri != null)
            {
                using var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync(_options.TelegramPingUri);
                response.EnsureSuccessStatusCode();
                telegramTook = (int)(DateTime.Now - telegram).TotalMilliseconds;
            }

            _logger.LogInformation(
                $"{title}: index {indexTook} ms, authorization {authorizationTook} ms, telegram {telegramTook} ms");
        }
    }

    public class MyInfo
    {
        public MyScheduleStatus ScheduleStatus { get; set; }

        public bool IsPastDue { get; set; }
    }

    public class MyScheduleStatus
    {
        public DateTime Last { get; set; }

        public DateTime Next { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}
