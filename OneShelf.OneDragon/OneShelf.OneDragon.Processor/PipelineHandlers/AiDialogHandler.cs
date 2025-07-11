﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.OpenAi.Services;
using OneShelf.OneDragon.Database;
using OneShelf.OneDragon.Database.Model;
using OneShelf.OneDragon.Database.Model.Enums;
using OneShelf.OneDragon.Processor.Model;
using OneShelf.OneDragon.Processor.Services;
using OneShelf.Telegram.Ai.Model;
using OneShelf.Telegram.Ai.PipelineHandlers;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI;
using Update = Telegram.BotAPI.GettingUpdates.Update;

namespace OneShelf.OneDragon.Processor.PipelineHandlers;

public class AiDialogHandler : AiDialogHandlerBase<InteractionType>
{
    private readonly DragonDatabase _dragonDatabase;
    private readonly DragonScope _dragonScope;
    private readonly Availability _availability;
    private readonly IOptions<TelegramOptions> _options;

    public AiDialogHandler(IScopedAbstractions scopedAbstractions,
        ILogger<AiDialogHandlerBase<InteractionType>> logger,
        DialogRunner dialogRunner,
        DragonDatabase dragonDatabase,
        DragonScope dragonScope,
        Availability availability,
        IOptions<TelegramOptions> options, 
        IHttpClientFactory httpClientFactory,
        Transcriber transcriber)
        : base(scopedAbstractions, logger, dragonDatabase, dialogRunner, httpClientFactory, transcriber)
    {
        _dragonDatabase = dragonDatabase;
        _dragonScope = dragonScope;
        _availability = availability;
        _options = options;
    }

    protected override void OnInitializing(long userId, long chatId)
    {
        _dragonDatabase.InitializeInteractionsRepositoryScope(userId, chatId);
    }

    protected override bool TraceImages => true;

    protected override IInteraction<InteractionType> CreateInteraction(Update update) => new Interaction
    {
        ChatId = update.Message?.Chat.Id ?? update.CallbackQuery!.Message!.Chat.Id,
        UpdateId = update.UpdateId,
    };

    protected override bool CheckRelevant(Update update)
    {
        if (update.Message?.From == null) return false;
        if (update.Message.Chat.Type != ChatTypes.Private) return false;

        return true;
    }

    protected override async Task<DateTime?> GetImagesUnavailableUntil(DateTime now)
        => await _availability.GetImagesUnavailableUntil(now);

    protected override async Task<DateTime?> GetVideosUnavailableUntil(DateTime now)
        => await _availability.GetVideosUnavailableUntil(now);

    protected override async Task<DateTime?> GetMusicUnavailableUntil(DateTime now)
        => await _availability.GetSongsUnavailableUntil(now);

    protected override async Task<DateTime?> GetChatUnavailableUntil()
    {
        var user = await _dragonDatabase.Users.SingleAsync(x => x.Id == _dragonScope.UserId);
        if (!user.UseLimits) return null;

        var limits = await _dragonDatabase.Limits.Where(x => x.Texts.HasValue).ToListAsync();
        if (!limits.Any()) return null;

        var now = DateTime.Now;
        DateTime Since(TimeSpan window) => now.Add(-window);

        var textsSince = Since(limits.Max(x => x.Window));
        var texts = (await _dragonDatabase.Interactions
                .Where(x => x.UserId == _dragonScope.UserId && x.ChatId == _dragonScope.ChatId)
                .Where(x => x.InteractionType == InteractionType.AiMessage || x.InteractionType == InteractionType.AiImageMessage)
                .Where(x => x.CreatedOn >= textsSince)
                .ToListAsync())
            .Select(x => x.CreatedOn)
            .ToList();

        DateTime? textsUnavailableUntil = null;
        foreach (var limit in limits)
        {
            if (texts.Count(x => x >= Since(limit.Window)) >= limit.Texts!.Value)
            {
                textsUnavailableUntil ??= DateTime.MinValue;
                var value = texts.Min().Add(limit.Window);
                textsUnavailableUntil = textsUnavailableUntil > value ? textsUnavailableUntil : value;
            }
        }

        return textsUnavailableUntil;
    }

    protected override string UnavailableUntilTemplate =>
        @"Я пока прилягу отдохнуть, а ты возвращайся {0} UTC. Надеюсь, лапки отдохнут к тому времени! Использовать сервис сверх лимита пока нельзя, но мой программист работает, не покладая лап, чтобы прикрутить оплату картами Украины, РФ, РБ и других стран (примерно с 1 ноября). Вот больше инфо - /help.

А я пока пороюсь в мусорке, вдруг там есть ответы на все вопросы. 🐾";

    protected override async Task<(string? system, string? version, float? frequencyPenalty, float? presencePenalty, int? imagesVersion, string? videoModel, string? musicModel)> GetAiParameters()
    {
        var aiParameters = await _dragonDatabase.AiParameters.SingleAsync();
        return (
            aiParameters.SystemMessage,
            aiParameters.GptVersion,
            aiParameters.FrequencyPenalty,
            aiParameters.PresencePenalty,
            aiParameters.DalleVersion,
            aiParameters.SoraModel,
            aiParameters.LyriaModel);
    }

    protected override (string? additionalBillingInfo, int? domainId) GetDialogConfigurationParameters() => ("one dragon", -1);

    protected override string ResponseError => "Случилась ошибка. :(";
}