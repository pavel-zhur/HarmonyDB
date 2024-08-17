using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Services;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.Runner.Functions.Functions;

public class BotFunctions
{
    private const string QueueNameUpdates = "updates";
    private const string QueueNameRegenerations = "regenerations";
    private const string SecretTokenHeaderName = "X-Telegram-Bot-Api-Secret-Token";

    private readonly ILogger<BotFunctions> _logger;
    private readonly Pipeline _pipeline;
    private readonly DailySelection _dailySelection;
    private readonly TelegramOptions _telegramOptions;
    private readonly RegenerationQueue _regenerationQueue;

    public BotFunctions(ILogger<BotFunctions> logger, IOptions<TelegramOptions> telegramOptions, Pipeline pipeline, DailySelection dailySelection, RegenerationQueue regenerationQueue)
    {
        _logger = logger;
        _pipeline = pipeline;
        _dailySelection = dailySelection;
        _regenerationQueue = regenerationQueue;
        _telegramOptions = telegramOptions.Value;
    }

    [Function("RegenerationsQueueTrigger")]
    public async Task RegenerationsQueueTrigger(
        [QueueTrigger(QueueNameRegenerations)] string myQueueItem)
    {
        if (!bool.Parse(myQueueItem)) return;
        await _regenerationQueue.QueueUpdateAllSync();
    }

    [Function("UpdatesQueueTrigger")]
    public async Task UpdatesQueueTrigger(
        [QueueTrigger(QueueNameUpdates)] string myQueueItem)
    {
        var update = JsonSerializer.Deserialize<Update>(myQueueItem) ?? throw new("Empty request body.");
        await await _pipeline.ProcessSyncSafeAndDispose(update);
    }

    [Function(nameof(Regenerate))]
    [QueueOutput(QueueNameRegenerations)]
    public async Task<bool> Regenerate(
        [HttpTrigger(AuthorizationLevel.Function, "get")]
        HttpRequestData req)
    {
        return true;
    }

    [Function("Hourly")]
    [QueueOutput(QueueNameRegenerations)]
    public async Task<bool> Hourly([TimerTrigger("0 15 * * * *")] TimerInfo myTimer) // Once every hour of the day at minute 15 of each hour
    {
        return true;
    }

    [Function("DailySelection")]
    public async Task DailySelection([TimerTrigger("0 34 9 * * *")] TimerInfo myTimer) // Daily at 9:34am UTC (12:34 utc+3)
    {
        await _dailySelection.DailySelectionGo();
    }

    [Function("DailySelectionTest")]
    public async Task<HttpResponseData> DailySelectionTest([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        var response = await _dailySelection.DailySelectionTest();

        var httpResponse = req.CreateResponse(HttpStatusCode.OK);
        await httpResponse.WriteStringAsync(response);

        return httpResponse;
    }

    [Function(nameof(IncomingUpdate))]
    [QueueOutput(QueueNameUpdates)]
    public async Task<string> IncomingUpdate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        if (!req.Headers.GetValues(SecretTokenHeaderName).Contains(_telegramOptions.WebHooksSecretToken)) throw new("Bad secret token.");

        var requestBody = await req.ReadAsStringAsync() ?? throw new("Empty request body.");

        return requestBody;
    }
}