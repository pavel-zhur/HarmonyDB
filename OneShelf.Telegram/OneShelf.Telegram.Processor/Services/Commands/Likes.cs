using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Pdfs.Generation.Inspiration.Services;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Processor.Model.Ios;
using OneShelf.Telegram.Processor.Services.Commands.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;

namespace OneShelf.Telegram.Processor.Services.Commands;

[BothCommand("likes", "Ваш шортлист", "Все песни которые вы лайкнули, ваш шортлист")]
public class Likes : Command
{
    private readonly ILogger<Likes> _logger;
    private readonly MessageMarkdownCombiner _messageMarkdownCombiner;
    private readonly InspirationGeneration _inspirationGeneration;
    private readonly TelegramOptions _telegramOptions;

    public Likes(ILogger<Likes> logger, Io io, MessageMarkdownCombiner messageMarkdownCombiner,
        InspirationGeneration inspirationGeneration, IOptions<TelegramOptions> telegramOptions,
        IOptions<TelegramOptions> options)
        : base(io, options)
    {
        _logger = logger;
        _messageMarkdownCombiner = messageMarkdownCombiner;
        _inspirationGeneration = inspirationGeneration;
        _telegramOptions = telegramOptions.Value;
    }

    protected override async Task ExecuteQuickly()
    {
        var messages = await _messageMarkdownCombiner.Shortlists(Io.UserId);
        if (!messages.Any())
        {
            Io.WriteLine("Вы не лайкнули ни одной песни.");
            return;
        }

        foreach (var (_, message, _, _) in messages)
        {
            Io.AdditionalOutput(message.BodyOrCaption);
        }

        Scheduled(Background());
    }

    private async Task Background()
    {
        var pdfFile = await _inspirationGeneration.Inspiration(_telegramOptions.TenantId, Io.UserId);

        var api = new TelegramBotClient(_telegramOptions.Token);
        await api.SendDocumentAsync(Io.UserId, new InputFile(pdfFile, "Likes.pdf"), caption: "Inspiration");
    }
}