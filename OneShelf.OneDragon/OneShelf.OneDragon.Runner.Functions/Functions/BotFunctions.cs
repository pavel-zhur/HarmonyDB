using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.OneDragon.Processor.Model;
using OneShelf.Telegram.Services;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDragon.Runner.Functions.Functions;

public class BotFunctions
{
    private const string QueueNameUpdates = "updates";
    private const string SecretTokenHeaderName = "X-Telegram-Bot-Api-Secret-Token";

    private readonly ILogger<BotFunctions> _logger;
    private readonly Pipeline _pipeline;
    private readonly TelegramOptions _options;

    public BotFunctions(ILogger<BotFunctions> logger, Pipeline pipeline, IOptions<TelegramOptions> options)
    {
        _logger = logger;
        _pipeline = pipeline;
        _options = options.Value;
    }

    [Function("UpdatesQueueTrigger")]
    public async Task UpdatesQueueTrigger(
        [QueueTrigger(QueueNameUpdates)] string myQueueItem)
    {
        var update = JsonSerializer.Deserialize<Update>(myQueueItem) ?? throw new("Empty request body.");
        await await _pipeline.ProcessSyncSafeAndDispose(update, 0);
    }

    [Function(nameof(IncomingUpdate))]
    [QueueOutput(QueueNameUpdates)]
    public async Task<string> IncomingUpdate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        if (!req.Headers.GetValues(SecretTokenHeaderName).Contains(_options.WebHooksSecretToken)) throw new("Bad secret token.");

        var requestBody = await req.ReadAsStringAsync() ?? throw new("Empty request body.");

        return requestBody;
    }
}