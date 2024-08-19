using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.OpenAi.Services;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Services.Base;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Telegram.Ai.Model;
using OneShelf.Telegram.Ai.PipelineHandlers;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.Processor.Services.PipelineHandlers;

public class AiDialogHandler : AiDialogHandlerBase<InteractionType>
{
    private readonly SongsDatabase _songsDatabase;
    private new readonly TelegramOptions _telegramOptions;

    public AiDialogHandler(
        ILogger<AiDialogHandler> logger,
        IOptions<TelegramOptions> telegramOptions,
        SongsDatabase songsDatabase,
        DialogRunner dialogRunner, 
        IScopedAbstractions scopedAbstractions)
        : base(scopedAbstractions, logger, songsDatabase, dialogRunner)
    {
        _songsDatabase = songsDatabase;
        _telegramOptions = telegramOptions.Value;
    }
    
    protected override bool CheckRelevant(Update update)
    {
        if (update.Message?.Chat.Username != _telegramOptions.PublicChatId.Substring(1)) return false;
        if (update.Message.MessageThreadId != _telegramOptions.OwnChatterTopicId) return false;

        return true;
    }

    protected override IInteraction<InteractionType> CreateInteraction(Update update) => new Interaction();

    protected override (string? additionalBillingInfo, int? domainId) GetDialogConfigurationParameters() => default;

    protected override async Task<DateTime?> GetImagesUnavailableUntil(DateTime now) => null;

    protected override async Task<(string? system, string? version, float? frequencyPenalty, float? presencePenalty, int? imagesVersion)> GetAiParameters()
    {
        var parameters = await _songsDatabase.Interactions
            .Where(x => x.InteractionType == InteractionType.OwnChatterSystemMessage
                        || x.InteractionType == InteractionType.OwnChatterVersion
                        || x.InteractionType == InteractionType.OwnChatterImagesVersion
                        || x.InteractionType == InteractionType.OwnChatterFrequencyPenalty
                        || x.InteractionType == InteractionType.OwnChatterPresencePenalty)
            .GroupBy(x => x.InteractionType)
            .Select(x => x.OrderByDescending(x => x.CreatedOn).FirstOrDefault())
            .ToListAsync();

        var system = parameters.SingleOrDefault(x => x.InteractionType == InteractionType.OwnChatterSystemMessage)?.Serialized;
        var version = parameters.SingleOrDefault(x => x.InteractionType == InteractionType.OwnChatterVersion)?.Serialized;
        var frequencyPenalty = parameters.SingleOrDefault(x => x.InteractionType == InteractionType.OwnChatterFrequencyPenalty)?.Serialized?.SelectSingle(x => float.TryParse(x, out var value) ? (float?)value : null);
        var presencePenalty = parameters.SingleOrDefault(x => x.InteractionType == InteractionType.OwnChatterPresencePenalty)?.Serialized?.SelectSingle(x => float.TryParse(x, out var value) ? (float?)value : null);
        var imagesVersion = parameters.SingleOrDefault(x => x.InteractionType == InteractionType.OwnChatterImagesVersion)?.Serialized?.SelectSingle(x => int.TryParse(x, out var value) ? (int?)value : null);
        return (system, version, frequencyPenalty, presencePenalty, imagesVersion);
    }
}