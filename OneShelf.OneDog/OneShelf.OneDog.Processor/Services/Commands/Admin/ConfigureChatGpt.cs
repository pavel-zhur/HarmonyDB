using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.OneDog.Database;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.OneDog.Processor.Services.Commands.Base;

namespace OneShelf.OneDog.Processor.Services.Commands.Admin;

[Command("a_cgs", "GPT", Role.Admin)]
public class ConfigureChatGpt : Command
{
    private readonly ILogger<ConfigureChatGpt> _logger;
    private readonly DogDatabase _dogDatabase;

    public ConfigureChatGpt(ILogger<ConfigureChatGpt> logger, Io io, DogDatabase dogDatabase, ScopeAwareness scopeAwareness)
        : base(io, scopeAwareness)
    {
        _logger = logger;
        _dogDatabase = dogDatabase;
    }

    private enum ConfigType
    {
        OwnChatterVersion,
        OwnChatterImagesVersion,
        OwnChatterFrequencyPenalty,
        OwnChatterPresencePenalty,
    }

    protected override async Task ExecuteQuickly()
    {
        var setting = Io.StrictChoice<ConfigType>("Тип");

        Io.WriteLine("Текущее значение:");
        Io.WriteLine();
        Io.WriteLine(setting switch
        {
            ConfigType.OwnChatterVersion => ScopeAwareness.Domain.GptVersion,
            ConfigType.OwnChatterImagesVersion => ScopeAwareness.Domain.DalleVersion.ToString(),
            ConfigType.OwnChatterFrequencyPenalty => ScopeAwareness.Domain.FrequencyPenalty?.ToString() ?? "нету",
            ConfigType.OwnChatterPresencePenalty => ScopeAwareness.Domain.PresencePenalty?.ToString() ?? "нету",
            _ => throw new ArgumentOutOfRangeException(),
        });
        Io.WriteLine();

        var newMessage = Io.FreeChoice("Новое значение: ");

        switch (setting)
        {
            case ConfigType.OwnChatterVersion:
                ScopeAwareness.Domain.GptVersion = newMessage;
                break;
            case ConfigType.OwnChatterImagesVersion:
                ScopeAwareness.Domain.DalleVersion = int.Parse(newMessage);
                break;
            case ConfigType.OwnChatterFrequencyPenalty:
                ScopeAwareness.Domain.FrequencyPenalty = float.Parse(newMessage);
                break;
            case ConfigType.OwnChatterPresencePenalty:
                ScopeAwareness.Domain.PresencePenalty = float.Parse(newMessage);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        await _dogDatabase.SaveChangesAsync();

        Io.WriteLine("Готово.");
    }
}