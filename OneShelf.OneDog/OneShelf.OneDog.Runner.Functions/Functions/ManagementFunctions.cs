using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OneShelf.Common.Api;
using System.Net;
using System.Text.Json;
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

        [Function(nameof(Check), AuthorizationLevel.Function, "get")]
        public async Task<IActionResult> Check(HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            return new ContentResult { Content = "Welcome to Azure Functions!", ContentType = "text/plain; charset=utf-8" };
        }

        [Function(nameof(CheckDb), AuthorizationLevel.Function, "get")]
        public async Task<IActionResult> CheckDb(HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            return new ContentResult { Content = $"Welcome to Azure Functions! Domains Count = {await _dogDatabase.Domains.CountAsync()}", ContentType = "text/plain; charset=utf-8" };
        }

        [Function(nameof(MigrateDb), AuthorizationLevel.Function, "get")]
        public async Task<IActionResult> MigrateDb(HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            await _dogDatabase.Database.MigrateAsync();

            return new ContentResult { Content = "Done.", ContentType = "text/plain; charset=utf-8" };
        }

        [Function(nameof(SetWebHook), AuthorizationLevel.Function, "get")]
        public async Task<IActionResult> SetWebHook(HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var (domainClient, domain) = await CreateDomainClient(GetDomainId(req));
            var secretToken = Guid.NewGuid().ToString();

            var result = (await domainClient.SetWebhookAsync($"https://{req.Host.Host}{req.PathBase}/{FunctionAttribute.RoutePrefix}/{nameof(BotFunctions.IncomingUpdate)}", secretToken: secretToken)).ToString();

            domain.WebHooksSecretToken = secretToken;
            await _dogDatabase.SaveChangesAsync();

            return new ContentResult { Content = result, ContentType = "text/plain; charset=utf-8" };
        }

        [Function(nameof(DeleteWebHook), AuthorizationLevel.Function, "get")]
        public async Task<IActionResult> DeleteWebHook(HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var (domainClient, _) = await CreateDomainClient(GetDomainId(req));
            return new ContentResult { Content = (await domainClient.DeleteWebhookAsync()).ToString(), ContentType = "text/plain; charset=utf-8" };
        }

        [Function(nameof(GetWebHook), AuthorizationLevel.Function, "get")]
        public async Task<IActionResult> GetWebHook(HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var (domainClient, _) = await CreateDomainClient(GetDomainId(req));
            return new ContentResult { Content = JsonSerializer.Serialize(await domainClient.GetWebhookInfoAsync()), ContentType = "text/plain; charset=utf-8" };
        }

        private async Task<(TelegramBotClient api, Domain domain)> CreateDomainClient(int domainId)
        {
            var domain = await _dogDatabase.Domains.SingleAsync(x => x.Id == domainId);
            return (new(domain.BotToken), domain);
        }

        private static int GetDomainId(HttpRequest req)
        {
            var domainId = int.TryParse(req.Query["domainId"], out var x)
                ? x
                : throw new("The domainId integer get parameter is required.");
            return domainId;
        }
    }
}
