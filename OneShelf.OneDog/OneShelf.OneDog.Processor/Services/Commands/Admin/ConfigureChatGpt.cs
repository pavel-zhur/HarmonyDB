using Microsoft.Extensions.Logging;
using OneShelf.OneDog.Database;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.OneDog.Processor.Services.Commands.Admin;

[Command("a_cgs", "GPT", Role.Admin)]
public class ConfigureChatGpt : Command
{
    private readonly ILogger<ConfigureChatGpt> _logger;
    private readonly DogDatabase _dogDatabase;
    private readonly DogContext _dogContext;

    public ConfigureChatGpt(ILogger<ConfigureChatGpt> logger, Io io, DogDatabase dogDatabase, DogContext dogContext)
        : base(io)
    {
        _logger = logger;
        _dogDatabase = dogDatabase;
        _dogContext = dogContext;
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
            ConfigType.OwnChatterVersion => _dogContext.Domain.GptVersion,
            ConfigType.OwnChatterImagesVersion => _dogContext.Domain.DalleVersion.ToString(),
            ConfigType.OwnChatterFrequencyPenalty => _dogContext.Domain.FrequencyPenalty?.ToString() ?? "нету",
            ConfigType.OwnChatterPresencePenalty => _dogContext.Domain.PresencePenalty?.ToString() ?? "нету",
            _ => throw new ArgumentOutOfRangeException(),
        });
        Io.WriteLine();

        var newMessage = Io.FreeChoice("Новое значение: ");

        switch (setting)
        {
            case ConfigType.OwnChatterVersion:
                _dogContext.Domain.GptVersion = newMessage;
                break;
            case ConfigType.OwnChatterImagesVersion:
                _dogContext.Domain.DalleVersion = int.Parse(newMessage);
                break;
            case ConfigType.OwnChatterFrequencyPenalty:
                _dogContext.Domain.FrequencyPenalty = float.Parse(newMessage);
                break;
            case ConfigType.OwnChatterPresencePenalty:
                _dogContext.Domain.PresencePenalty = float.Parse(newMessage);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        await _dogDatabase.SaveChangesAsync();

        Io.WriteLine("Готово.");
    }
}