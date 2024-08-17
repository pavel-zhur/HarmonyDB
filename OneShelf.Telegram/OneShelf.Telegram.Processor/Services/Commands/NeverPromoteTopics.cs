using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Illustrations.Api.Client;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.Stickers;
using TelegramOptions = OneShelf.Telegram.Processor.Model.TelegramOptions;

namespace OneShelf.Telegram.Processor.Services.Commands;

[BothCommand("never_promote", "Не предлагайте!")]
public class NeverPromoteTopics : Command
{
    private readonly ILogger<NeverPromoteTopics> _logger;
    private readonly MessageMarkdownCombiner _messageMarkdownCombiner;
    private readonly SongsDatabase _songsDatabase;
    private readonly IllustrationsApiClient _illustrationsApiClient;
    private readonly FullTextSearch _fullTextSearch;
    private readonly TelegramBotClient _botClient;
    private readonly TelegramOptions _options;

    public NeverPromoteTopics(ILogger<NeverPromoteTopics> logger, Io io,
        MessageMarkdownCombiner messageMarkdownCombiner, SongsDatabase songsDatabase,
        IllustrationsApiClient illustrationsApiClient, IOptions<TelegramOptions> options, FullTextSearch fullTextSearch)
        : base(io)
    {
        _logger = logger;
        _messageMarkdownCombiner = messageMarkdownCombiner;
        _songsDatabase = songsDatabase;
        _illustrationsApiClient = illustrationsApiClient;
        _fullTextSearch = fullTextSearch;
        _botClient = new(options.Value.Token);
        _options = options.Value;
    }

    protected override async Task ExecuteQuickly()
    {
        if (await _songsDatabase.Interactions.AnyAsync(x =>
                x.UserId == Io.UserId && x.InteractionType == InteractionType.NeverPromoteTopics))
        {
            try
            {
                await _botClient.SendStickerAsync(Io.UserId, _options.NeverPromoteResponseStickerFileId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error sending the sticker.");
            }

            return;
        }

        Io.WriteLine("Добрый день!");
        Io.WriteLine();
        Io.WriteLine("Если использовать эту команду, я не буду вас больше выбирать в игре, в которой иногда у случайных людей появляется возможность делать тематические картинки к песням.");
        Io.WriteLine();
        var choice = Io.StrictChoice<Confirmation>("Вы уверены, что не хотите, чтобы я выбирала вас в этой игре 🐾🎶?");

        if (choice == Confirmation.No)
        {
            return;
        }

        _songsDatabase.Interactions.Add(new()
        {
            UserId = Io.UserId,
            InteractionType = InteractionType.NeverPromoteTopics,
            CreatedOn = DateTime.Now,
            Serialized = "yep",
        });
        await _songsDatabase.SaveChangesAsyncX();

        Io.WriteLine("Окей. Больше не буду. Обещаю. 🐾");
    }
}