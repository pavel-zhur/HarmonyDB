using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Database.Model;
using Telegram.BotAPI;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDog.Runner.Functions.Functions
{
    public class ManagementFunctions
    {
        private readonly DogDatabase _dogDatabase;
        private readonly ILogger<ManagementFunctions> _logger;

        public ManagementFunctions(ILogger<ManagementFunctions> logger, DogDatabase dogDatabase)
        {
            _dogDatabase = dogDatabase;
            _logger = logger;
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

        [Function(nameof(CheckDb))]
        public async Task<HttpResponseData> CheckDb([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync($"Welcome to Azure Functions! Domains Count = {await _dogDatabase.Domains.CountAsync()}");

            return response;
        }

        [Function(nameof(MigrateDb))]
        public async Task<HttpResponseData> MigrateDb([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            await _dogDatabase.Database.MigrateAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync("Done.");

            return response;
        }

        [Function(nameof(SetWebHook))]
        public async Task<HttpResponseData> SetWebHook([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            var (domainClient, domain) = await CreateDomainClient(GetDomainId(req));
            var secretToken = Guid.NewGuid().ToString();

            await response.WriteStringAsync((await domainClient.SetWebhookAsync($"https://{req.Url.Host}/api/{nameof(BotFunctions.IncomingUpdate)}", secretToken: secretToken)).ToString());

            domain.WebHooksSecretToken = secretToken;
            await _dogDatabase.SaveChangesAsync();

            return response;
        }

        [Function(nameof(DeleteWebHook))]
        public async Task<HttpResponseData> DeleteWebHook([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            var (domainClient, _) = await CreateDomainClient(GetDomainId(req));
            await response.WriteStringAsync((await domainClient.DeleteWebhookAsync()).ToString());

            return response;
        }

        [Function(nameof(GetWebHook))]
        public async Task<HttpResponseData> GetWebHook([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            var (domainClient, _) = await CreateDomainClient(GetDomainId(req));
            await response.WriteStringAsync(JsonSerializer.Serialize(await domainClient.GetWebhookInfoAsync()));

            return response;
        }

        private async Task<(TelegramBotClient api, Domain domain)> CreateDomainClient(int domainId)
        {
            var domain = await _dogDatabase.Domains.SingleAsync(x => x.Id == domainId);
            return (new(domain.BotToken), domain);
        }

        private static int GetDomainId(HttpRequestData req)
        {
            var domainId = int.TryParse(req.Query["domainId"], out var x)
                ? x
                : throw new("The domainId integer get parameter is required.");
            return domainId;
        }
    }
}
