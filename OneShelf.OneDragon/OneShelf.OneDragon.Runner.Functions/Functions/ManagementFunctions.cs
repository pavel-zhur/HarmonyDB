using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OneShelf.Common.Api;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.OneDragon.Database;
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
        private readonly DragonDatabase _dragonDatabase;

        public ManagementFunctions(ILogger<ManagementFunctions> logger, IOptions<TelegramOptions> telegramOptions, DragonDatabase dragonDatabase)
        {
            _logger = logger;
            _dragonDatabase = dragonDatabase;
            _telegramOptions = telegramOptions.Value;
            _api = new(_telegramOptions.Token);
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

            return new ContentResult { Content = $"Welcome to Azure Functions! Users Count = {await _dragonDatabase.Users.CountAsync()}", ContentType = "text/plain; charset=utf-8" };
        }

        [Function(nameof(MigrateDb), AuthorizationLevel.Function, "get")]
        public async Task<IActionResult> MigrateDb(HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            await _dragonDatabase.Database.MigrateAsync();

            return new ContentResult { Content = "Done.", ContentType = "text/plain; charset=utf-8" };
        }

        [Function(nameof(SetWebHook), AuthorizationLevel.Anonymous, "get")]
        public async Task<IActionResult> SetWebHook(HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            return new ContentResult { Content = (await _api.SetWebhookAsync($"https://{req.Host.Host}{req.PathBase}/{FunctionAttribute.RoutePrefix}/{nameof(BotFunctions.IncomingUpdate)}", secretToken: _telegramOptions.WebHooksSecretToken)).ToString(), ContentType = "text/plain; charset=utf-8" };
        }

        [Function(nameof(DeleteWebHook), AuthorizationLevel.Anonymous, "get")]
        public async Task<IActionResult> DeleteWebHook(HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            return new ContentResult { Content = (await _api.DeleteWebhookAsync()).ToString(), ContentType = "text/plain; charset=utf-8" };
        }

        [Function(nameof(GetWebHook), AuthorizationLevel.Anonymous, "get")]
        public async Task<IActionResult> GetWebHook(HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            return new ContentResult { Content = JsonSerializer.Serialize(await _api.GetWebhookInfoAsync()), ContentType = "text/plain; charset=utf-8" };
        }
    }
}
