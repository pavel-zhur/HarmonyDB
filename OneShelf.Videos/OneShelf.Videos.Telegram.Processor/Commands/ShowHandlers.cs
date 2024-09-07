using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;
using OneShelf.Videos.Database;
using OneShelf.Videos.Telegram.Processor.Model;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;

namespace OneShelf.Videos.Telegram.Processor.Commands;

[AdminCommand("show_handlers", "Хендлеры", "Показать хендлеры")]
public class ShowHandlers : Command
{
    private readonly VideosDatabase _videosDatabase;
    private readonly IOptions<TelegramOptions> _options;
    private readonly TelegramBotClient _botClient;

    public ShowHandlers(Io io, VideosDatabase videosDatabase, IOptions<TelegramOptions> options) 
        : base(io)
    {
        _videosDatabase = videosDatabase;
        _options = options;
        _botClient = new(_options.Value.Token);
    }

    protected override async Task ExecuteQuickly()
    {
        Scheduled(Background());
    }

    private async Task Background()
    {
        var items = await _videosDatabase.TelegramMedia.Where(x => !x.HandlerMessageId.HasValue).ToListAsync();
        var chunks = items
            .OrderBy(x => x.TelegramUpdateId)
            .WithPrevious()
            .ToChunksByShouldStartNew(p =>
                p.previous == null || Math.Abs((p.current.CreatedOn - p.previous.CreatedOn).TotalMinutes) > 1)
            .Select(x => x.Select(x => x.current).ToList())
            .ToList();

        foreach (var chunk in chunks)
        {
            var message = await _botClient.SendMessageAsync(
                chunk[0].ChatId,
                "#handler " + string.Join(
                    " + ",
                    chunk
                        .WithPrevious()
                        .ToChunksByShouldStartNew(p => p.current.MediaGroupId != p.previous?.MediaGroupId)
                        .Select(g => g.Count)),
                replyParameters: new()
                {
                    ChatId = chunk[0].ChatId,
                    MessageId = chunk[0].MessageId,
                },
                replyMarkup: new InlineKeyboardMarkup([
                    [
                        new("button1")
                        {
                            CallbackData = "button1",
                        }
                    ]
                ]));

            chunk.ForEach(m => m.HandlerMessageId = message.MessageId);
            await _videosDatabase.SaveChangesAsync();
        }
    }
}