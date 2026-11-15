using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Api;
using OneShelf.Common.Api.Background;
using OneShelf.OneDragon.Processor.Model;

namespace OneShelf.OneDragon.Runner.Functions.Functions;

public class BotFunctions
{
    private const string SecretTokenHeaderName = "X-Telegram-Bot-Api-Secret-Token";

    private readonly ILogger<BotFunctions> _logger;
    private readonly TelegramOptions _options;
    private readonly BackgroundQueue<string> _updates;

    public BotFunctions(ILogger<BotFunctions> logger, IOptions<TelegramOptions> options, BackgroundQueue<string> updates)
    {
        _logger = logger;
        _updates = updates;
        _options = options.Value;
    }

    /// <summary>
    /// Accepts an update from Telegram at once; it is processed in the background (see <see cref="Background.UpdatesWork"/>).
    /// </summary>
    [Function(nameof(IncomingUpdate), AuthorizationLevel.Anonymous, "post")]
    public async Task<IActionResult> IncomingUpdate(HttpRequest req)
    {
        if (!req.Headers[SecretTokenHeaderName].Contains(_options.WebHooksSecretToken)) throw new("Bad secret token.");

        using var reader = new StreamReader(req.Body);
        _updates.Enqueue(await reader.ReadToEndAsync());

        return new OkResult();
    }
}
