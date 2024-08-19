using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.OpenAi.Services;
using OneShelf.OneDragon.Database;
using OneShelf.OneDragon.Database.Model;
using OneShelf.OneDragon.Database.Model.Enums;
using OneShelf.Telegram.Ai.Model;
using OneShelf.Telegram.Ai.PipelineHandlers;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI;
using Update = Telegram.BotAPI.GettingUpdates.Update;

namespace OneShelf.OneDragon.Processor.PipelineHandlers;

public class AiDialogHandler : AiDialogHandlerBase<InteractionType>
{
    private readonly DragonDatabase _dragonDatabase;

    public AiDialogHandler(
        IScopedAbstractions scopedAbstractions,
        ILogger<AiDialogHandlerBase<InteractionType>> logger, 
        DialogRunner dialogRunner, 
        DragonDatabase dragonDatabase)
        : base(scopedAbstractions, logger, dragonDatabase, dialogRunner)
    {
        _dragonDatabase = dragonDatabase;
    }

    protected override void OnInitializing(Update update)
    {
        _dragonDatabase.InitializeInteractionsRepositoryScope(update.Message!.From!.Id, update.Message.Chat.Id);
    }

    protected override IInteraction<InteractionType> CreateInteraction(Update update) => new Interaction
    {
        ChatId = update.Message!.Chat.Id,
        UpdateId = update.UpdateId,
    };

    protected override bool CheckRelevant(Update update)
    {
        if (update.Message?.From == null) return false;
        if (update.Message.Chat.Type != ChatTypes.Private) return false;

        return true;
    }

    protected override async Task<DateTime?> GetImagesUnavailableUntil(DateTime now) => null;

    protected override async Task<(string? system, string? version, float? frequencyPenalty, float? presencePenalty, int? imagesVersion)> GetAiParameters()
    {
        var aiParameters = await _dragonDatabase.AiParameters.SingleAsync();
        return (
            aiParameters.SystemMessage,
            aiParameters.GptVersion,
            aiParameters.FrequencyPenalty,
            aiParameters.PresencePenalty,
            aiParameters.DalleVersion);
    }

    protected override (string? additionalBillingInfo, int? domainId) GetDialogConfigurationParameters() => ("one dragon", -1);
}