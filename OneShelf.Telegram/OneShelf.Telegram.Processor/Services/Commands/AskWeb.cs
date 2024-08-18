using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Illustrations.Api.Client;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.Stickers;

namespace OneShelf.Telegram.Processor.Services.Commands;

[BothCommand("ask_one_shelf", "Попросить доступ в шкаф")]
public class AskWeb : Command
{
    private readonly SongsDatabase _songsDatabase;
    private readonly TelegramOptions _options;

    public AskWeb(Io io, SongsDatabase songsDatabase, IOptions<TelegramOptions> options)
        : base(io)
    {
        _songsDatabase = songsDatabase;
        _options = options.Value;
    }

    protected override async Task ExecuteQuickly()
    {
        Io.WriteLine("Готово!");
        Io.WriteLine();
        Io.WriteLine("Можете еще написать @pavel_zhur в личку, чтобы было быстрее.");
        Io.WriteLine();

        _songsDatabase.Interactions.Add(new()
        {
            UserId = Io.UserId,
            InteractionType = InteractionType.AskWeb,
            CreatedOn = DateTime.Now,
            Serialized = _options.TenantId.ToString(),
        });
        await _songsDatabase.SaveChangesAsyncX();
    }
}