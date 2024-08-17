using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Database.Model.Enums;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.OneDog.Processor.Services.Commands.DomainAdmin;

[Command("a_cd", "Конфигурация", Role.DomainAdmin, "Личность и всё такое")]
public class ConfigureDog : Command
{
    private readonly ILogger<ConfigureDog> _logger;
    private readonly DogDatabase _dogDatabase;
    private readonly TelegramContext _telegramContext;

    public ConfigureDog(ILogger<ConfigureDog> logger, Io io, DogDatabase dogDatabase, TelegramContext telegramContext)
        : base(io)
    {
        _logger = logger;
        _dogDatabase = dogDatabase;
        _telegramContext = telegramContext;
    }

    private enum ConfigType
    {
        [Display(Name = "Личность")]
        OwnChatterSystemMessage,

        [Display(Name = "Амнезия диалога")]
        OwnChatterResetDialog,
    }

    protected override async Task ExecuteQuickly()
    {
        var setting = Io.StrictChoice<ConfigType>("Тип");

        if (setting == ConfigType.OwnChatterResetDialog)
        {
            _dogDatabase.Interactions.Add(new()
            {
                CreatedOn = DateTime.Now,
                InteractionType = InteractionType.OwnChatterResetDialog,
                UserId = Io.UserId,
                Serialized = "reset",
                DomainId = _telegramContext.DomainId,
            });

            await _dogDatabase.SaveChangesAsync();

            Io.WriteLine("Готово.");
            return;
        }

        Io.WriteLine("Текущее значение:");
        Io.WriteLine();
        Io.WriteLine(setting switch
        {
            ConfigType.OwnChatterSystemMessage => _telegramContext.Domain.SystemMessage,
            ConfigType.OwnChatterResetDialog or _ => throw new ArgumentOutOfRangeException(),
        });
        Io.WriteLine();

        var newMessage = Io.FreeChoice("Новое значение: ");

        switch (setting)
        {
            case ConfigType.OwnChatterSystemMessage:
                _telegramContext.Domain.SystemMessage = newMessage;
                break;
            case ConfigType.OwnChatterResetDialog:
            default:
                throw new ArgumentOutOfRangeException();
        }

        await _dogDatabase.SaveChangesAsync();

        Io.WriteLine("Готово.");
    }
}