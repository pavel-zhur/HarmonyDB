using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Database.Model.Enums;
using OneShelf.OneDog.Processor.Model;
using OneShelf.OneDog.Processor.Model.Ios;
using OneShelf.OneDog.Processor.Services.Commands.Base;

namespace OneShelf.OneDog.Processor.Services.Commands.DomainAdmin;

[Command("a_cd", "Конфигурация", Role.DomainAdmin, "Личность и всё такое")]
public class ConfigureDog : Command
{
    private readonly ILogger<ConfigureDog> _logger;
    private readonly DogDatabase _dogDatabase;

    public ConfigureDog(ILogger<ConfigureDog> logger, Io io, DogDatabase dogDatabase, ScopeAwareness scopeAwareness)
        : base(io, scopeAwareness)
    {
        _logger = logger;
        _dogDatabase = dogDatabase;
    }

    private enum ConfigType
    {
        [StrictChoiceCaption("Личность")]
        OwnChatterSystemMessage,

        [StrictChoiceCaption("Амнезия диалога")]
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
                DomainId = ScopeAwareness.DomainId,
            });

            await _dogDatabase.SaveChangesAsync();

            Io.WriteLine("Готово.");
            return;
        }

        Io.WriteLine("Текущее значение:");
        Io.WriteLine();
        Io.WriteLine(setting switch
        {
            ConfigType.OwnChatterSystemMessage => ScopeAwareness.Domain.SystemMessage,
            ConfigType.OwnChatterResetDialog or _ => throw new ArgumentOutOfRangeException(),
        });
        Io.WriteLine();

        var newMessage = Io.FreeChoice("Новое значение: ");

        switch (setting)
        {
            case ConfigType.OwnChatterSystemMessage:
                ScopeAwareness.Domain.SystemMessage = newMessage;
                break;
            case ConfigType.OwnChatterResetDialog:
            default:
                throw new ArgumentOutOfRangeException();
        }

        await _dogDatabase.SaveChangesAsync();

        Io.WriteLine("Готово.");
    }
}