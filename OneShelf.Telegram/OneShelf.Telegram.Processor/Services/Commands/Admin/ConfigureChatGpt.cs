using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Processor.Model.Ios;
using OneShelf.Telegram.Processor.Services.Commands.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_cgs", "Chat GPT")]
public class ConfigureChatGpt : Command
{
    private readonly ILogger<ConfigureChatGpt> _logger;
    private readonly SongsDatabase _songsDatabase;

    public ConfigureChatGpt(ILogger<ConfigureChatGpt> logger, Io io, SongsDatabase songsDatabase,
        IOptions<TelegramOptions> options)
        : base(io, options)
    {
        _logger = logger;
        _songsDatabase = songsDatabase;
    }

    protected override async Task ExecuteQuickly()
    {
        var setting = Io.StrictChoice<InteractionType>("Тип",
            isActionAvailable: x => x is InteractionType.OwnChatterSystemMessage or InteractionType.OwnChatterVersion or InteractionType.OwnChatterImagesVersion or InteractionType.OwnChatterFrequencyPenalty or InteractionType.OwnChatterPresencePenalty or InteractionType.OwnChatterResetDialog);

        string newMessage;
        if (setting == InteractionType.OwnChatterResetDialog)
        {
            newMessage = "reset";
        }
        else
        {
            var lastMessage = await _songsDatabase.Interactions.Where(x => x.InteractionType == setting)
                .OrderByDescending(x => x.CreatedOn).FirstOrDefaultAsync();

            if (lastMessage != null)
            {
                Io.WriteLine("Текущее значение:");
                Io.WriteLine();
                Io.WriteLine(lastMessage.Serialized);
                Io.WriteLine();
            }

            newMessage = Io.FreeChoice("Новое значение: ");
        }

        _songsDatabase.Interactions.Add(new()
        {
            CreatedOn = DateTime.Now,
            InteractionType = setting,
            UserId = Io.UserId,
            Serialized = newMessage,
        });
        await _songsDatabase.SaveChangesAsyncX();

        Io.WriteLine("Готово.");
    }
}