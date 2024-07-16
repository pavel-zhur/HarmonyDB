using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OneShelf.Common.Database.Songs;
using OneShelf.Telegram.Processor.Model;
using Telegram.BotAPI;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.Runner.Functions.Functions
{
    public class ManagementFunctions
    {
        private readonly SongsDatabase _songsDatabase;
        private readonly ILogger<ManagementFunctions> _logger;
        private readonly TelegramOptions _telegramOptions;
        private readonly BotClient _api;

        public ManagementFunctions(ILogger<ManagementFunctions> logger, SongsDatabase songsDatabase, IOptions<TelegramOptions> telegramOptions)
        {
            _songsDatabase = songsDatabase;
            _logger = logger;
            _telegramOptions = telegramOptions.Value;
            _api = new(_telegramOptions.Token);
        }

        [Function(nameof(Check))]
        public async Task<HttpResponseData> Check([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync("Welcome to Azure Functions!");

            return response;
        }

        [Function(nameof(CheckDb))]
        public async Task<HttpResponseData> CheckDb([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync($"Welcome to Azure Functions! Songs Count = {await _songsDatabase.Songs.CountAsync()}");

            return response;
        }

        [Function(nameof(MigrateDb))]
        public async Task<HttpResponseData> MigrateDb([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            await _songsDatabase.Database.MigrateAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync("Done.");

            return response;
        }

        [Function(nameof(SetWebHook))]
        public async Task<HttpResponseData> SetWebHook([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync((await _api.SetWebhookAsync($"https://{req.Url.Host}/api/{nameof(BotFunctions.IncomingUpdate)}", secretToken: _telegramOptions.WebHooksSecretToken)).ToString());

            return response;
        }

        [Function(nameof(DeleteWebHook))]
        public async Task<HttpResponseData> DeleteWebHook([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync((await _api.DeleteWebhookAsync()).ToString());

            return response;
        }

        [Function(nameof(GetWebHook))]
        public async Task<HttpResponseData> GetWebHook([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync(JsonConvert.SerializeObject(await _api.GetWebhookInfoAsync()));

            return response;
        }
    }
}
