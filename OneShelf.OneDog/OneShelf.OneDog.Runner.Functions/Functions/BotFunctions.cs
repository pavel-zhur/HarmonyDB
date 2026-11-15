using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Common.Api;
using OneShelf.Common.Api.Background;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Runner.Functions.Background;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDog.Runner.Functions.Functions;

public class BotFunctions
{
    private const string SecretTokenHeaderName = "X-Telegram-Bot-Api-Secret-Token";

    private readonly ILogger<BotFunctions> _logger;
    private readonly DogDatabase _dogDatabase;
    private readonly BackgroundQueue<DomainUpdate> _updates;

    public BotFunctions(ILogger<BotFunctions> logger, DogDatabase dogDatabase, BackgroundQueue<DomainUpdate> updates)
    {
        _logger = logger;
        _dogDatabase = dogDatabase;
        _updates = updates;
    }

    /// <summary>
    /// Accepts an update from Telegram for the bot (domain) whose secret token it carries at once; it is processed in
    /// the background (see <see cref="UpdatesWork"/>).
    /// </summary>
    [Function(nameof(IncomingUpdate), AuthorizationLevel.Anonymous, "post")]
    public async Task<IActionResult> IncomingUpdate(HttpRequest req)
    {
        var headers = req.Headers[SecretTokenHeaderName].ToList();
        if (headers.Count != 1) throw new("The secret token header is expected.");

        var secretToken = headers.Single();

        var domain = await _dogDatabase.Domains.SingleOrDefaultAsync(x => x.WebHooksSecretToken == secretToken) ?? throw new("Such domain is not found.");

        var requestBody = await req.ReadFromJsonAsync<Update>() ?? throw new("Empty request body.");

        _updates.Enqueue(new()
        {
            Update = requestBody,
            DomainId = domain.Id,
        });

        return new OkResult();
    }
}
