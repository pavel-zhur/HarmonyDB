using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Api;
using OneShelf.Common.Api.Background;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Services;

namespace OneShelf.Telegram.Runner.Functions.Functions;

public class BotFunctions
{
    private const string SecretTokenHeaderName = "X-Telegram-Bot-Api-Secret-Token";

    private readonly ILogger<BotFunctions> _logger;
    private readonly DailySelection _dailySelection;
    private readonly TelegramOptions _telegramOptions;
    private readonly RegenerationQueue _regenerationQueue;
    private readonly BackgroundQueue<string> _updates;

    public BotFunctions(ILogger<BotFunctions> logger, IOptions<TelegramOptions> telegramOptions, DailySelection dailySelection, RegenerationQueue regenerationQueue, BackgroundQueue<string> updates)
    {
        _logger = logger;
        _dailySelection = dailySelection;
        _regenerationQueue = regenerationQueue;
        _updates = updates;
        _telegramOptions = telegramOptions.Value;
    }

    [Function(nameof(Regenerate), AuthorizationLevel.Function, "get")]
    public IActionResult Regenerate(HttpRequest req)
    {
        _regenerationQueue.QueueUpdateAll(false);
        return new OkResult();
    }

    [Function("DailySelectionTest", AuthorizationLevel.Anonymous, "get")]
    public async Task<IActionResult> DailySelectionTest(HttpRequest req)
    {
        await req.HttpContext.Response.WriteAsync(await _dailySelection.DailySelectionTest());
        return new EmptyResult();
    }

    /// <summary>
    /// Accepts an update from Telegram at once; it is processed in the background (see <see cref="Background.UpdatesWork"/>).
    /// </summary>
    [Function(nameof(IncomingUpdate), AuthorizationLevel.Anonymous, "post")]
    public async Task<IActionResult> IncomingUpdate(HttpRequest req)
    {
        if (!req.Headers[SecretTokenHeaderName].Contains(_telegramOptions.WebHooksSecretToken)) throw new("Bad secret token.");

        using var reader = new StreamReader(req.Body);
        _updates.Enqueue(await reader.ReadToEndAsync());

        return new OkResult();
    }
}
