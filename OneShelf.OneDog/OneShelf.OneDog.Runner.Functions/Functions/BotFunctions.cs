using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Processor.Services;
using Telegram.BotAPI.GettingUpdates;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace OneShelf.OneDog.Runner.Functions.Functions;

public class BotFunctions
{
    private const string QueueNameUpdates = "updates";
    private const string SecretTokenHeaderName = "X-Telegram-Bot-Api-Secret-Token";

    private readonly ILogger<BotFunctions> _logger;
    private readonly Pipeline _pipeline;
    private readonly DogDatabase _dogDatabase;

    public BotFunctions(ILogger<BotFunctions> logger, Pipeline pipeline, DogDatabase dogDatabase)
    {
        _logger = logger;
        _pipeline = pipeline;
        _dogDatabase = dogDatabase;
    }
    
    [Function("UpdatesQueueTrigger")]
    public async Task UpdatesQueueTrigger(
        [QueueTrigger(QueueNameUpdates)] string myQueueItem)
    {
        var update = JsonSerializer.Deserialize<QueueMessage>(myQueueItem) ?? throw new("Empty request body.");
        await await _pipeline.ProcessSyncSafeAndDispose(update.Update, update.DomainId);
    }
    
    [Function(nameof(IncomingUpdate))]
    [QueueOutput(QueueNameUpdates)]
    public async Task<string> IncomingUpdate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        var headers = req.Headers.GetValues(SecretTokenHeaderName).ToList();
        if (headers.Count != 1) throw new("The secret token header is expected.");

        var secretToken = headers.Single();

        var domain = await _dogDatabase.Domains.SingleOrDefaultAsync(x => x.WebHooksSecretToken == secretToken) ?? throw new("Such domain is not found.");

        var requestBody = await req.ReadFromJsonAsync<Update>() ?? throw new("Empty request body.");

        return JsonSerializer.Serialize(new QueueMessage
        {
            Update = requestBody,
            DomainId = domain.Id,
        });
    }

    private class QueueMessage
    {
        public required Update Update { get; init; }

        public required int DomainId { get; init; }
    }
}