using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.OneDragon.Processor.Model;
using Telegram.BotAPI;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDragon.Runner.Functions.Functions
{
    public class ManagementFunctions
    {
        private readonly ILogger<ManagementFunctions> _logger;
        private readonly TelegramOptions _telegramOptions;
        private readonly TelegramBotClient _api;

        public ManagementFunctions(ILogger<ManagementFunctions> logger, IOptions<TelegramOptions> telegramOptions)
        {
            _logger = logger;
            _telegramOptions = telegramOptions.Value;
            _api = new(_telegramOptions.Token);
        }

        [Function(nameof(Check))]
        public async Task<HttpResponseData> Check([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync("Welcome to Azure Functions!");

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

            await response.WriteStringAsync(JsonSerializer.Serialize(await _api.GetWebhookInfoAsync()));

            return response;
        }
    }
}
