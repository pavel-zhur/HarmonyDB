using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.OneDog.Database;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.OneDog.Processor.Services.Commands.Admin;

[Command("a_cgs", "GPT", Role.Admin)]
public class ConfigureChatGpt : Command
{
    private readonly ILogger<ConfigureChatGpt> _logger;
    private readonly DogDatabase _dogDatabase;
    private readonly ScopeAwareness _scopeAwareness;

    public ConfigureChatGpt(ILogger<ConfigureChatGpt> logger, Io io, DogDatabase dogDatabase, ScopeAwareness scopeAwareness)
        : base(io)
    {
        _logger = logger;
        _dogDatabase = dogDatabase;
        _scopeAwareness = scopeAwareness;
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
            ConfigType.OwnChatterVersion => _scopeAwareness.Domain.GptVersion,
            ConfigType.OwnChatterImagesVersion => _scopeAwareness.Domain.DalleVersion.ToString(),
            ConfigType.OwnChatterFrequencyPenalty => _scopeAwareness.Domain.FrequencyPenalty?.ToString() ?? "нету",
            ConfigType.OwnChatterPresencePenalty => _scopeAwareness.Domain.PresencePenalty?.ToString() ?? "нету",
            _ => throw new ArgumentOutOfRangeException(),
        });
        Io.WriteLine();

        var newMessage = Io.FreeChoice("Новое значение: ");

        switch (setting)
        {
            case ConfigType.OwnChatterVersion:
                _scopeAwareness.Domain.GptVersion = newMessage;
                break;
            case ConfigType.OwnChatterImagesVersion:
                _scopeAwareness.Domain.DalleVersion = int.Parse(newMessage);
                break;
            case ConfigType.OwnChatterFrequencyPenalty:
                _scopeAwareness.Domain.FrequencyPenalty = float.Parse(newMessage);
                break;
            case ConfigType.OwnChatterPresencePenalty:
                _scopeAwareness.Domain.PresencePenalty = float.Parse(newMessage);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        await _dogDatabase.SaveChangesAsync();

        Io.WriteLine("Готово.");
    }
}