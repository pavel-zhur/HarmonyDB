﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.OpenAi.Services;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Database.Model.Enums;
using OneShelf.Telegram.Services.Base;
using OneShelf.Telegram.Ai.Model;
using OneShelf.Telegram.Ai.PipelineHandlers;
using Telegram.BotAPI.GettingUpdates;
using OneShelf.OneDog.Database.Model;

namespace OneShelf.OneDog.Processor.Services.PipelineHandlers;

public class AiDialogHandler : AiDialogHandlerBase<InteractionType>
{
    private readonly DogContext _dogContext;
    private readonly DogDatabase _dogDatabase;

    public AiDialogHandler(ILogger<AiDialogHandler> logger,
        DogDatabase dogDatabase,
        DialogRunner dialogRunner,
        IScopedAbstractions scopedAbstractions,
        DogContext dogContext,
        IHttpClientFactory httpClientFactory,
        Transcriber transcriber)
        : base(scopedAbstractions, logger, dogDatabase, dialogRunner, httpClientFactory, transcriber)
    {
        _dogDatabase = dogDatabase;
        _dogContext = dogContext;
    }

    protected override void OnInitializing(long userId, long chatId)
    {
        _dogDatabase.InitializeInteractionsRepositoryScope(_dogContext.DomainId);
    }

    protected override bool CheckRelevant(Update update)
    {
        if (update.Message?.Chat.Id != _dogContext.Domain.ChatId) return false;
        if (update.Message.MessageThreadId != _dogContext.Domain.TopicId) return false;

        return true;
    }

    protected override IInteraction<InteractionType> CreateInteraction(Update update) => new Interaction
    {
        DomainId = _dogContext.DomainId,
    };

    protected override (string? additionalBillingInfo, int? domainId) GetDialogConfigurationParameters()
    {
        var additionalBillingInfo = "one dog";
        var dogContextDomainId = _dogContext.DomainId;
        return (additionalBillingInfo, dogContextDomainId);
    }

    protected override string ResponseError => "Случилась ошибка. :(";

    protected override async Task<DateTime?> GetImagesUnavailableUntil(DateTime now)
    {
        DateTime? imagesUnavailableUntil = null;
        if (_dogContext.Domain.ImagesLimit != null)
        {
            var imagesSince = now.Add(-_dogContext.Domain.ImagesLimit.Window);
            var images = (await _dogDatabase.Interactions
                    .Where(x => x.InteractionType == InteractionType.ImagesSuccess)
                    .Where(x => x.CreatedOn >= imagesSince)
                    .ToListAsync())
                .Select(x => (x.CreatedOn, count: int.Parse(x.Serialized)))
                .ToList();

            if (images.Sum(x => x.count) >= _dogContext.Domain.ImagesLimit.Limit)
            {
                imagesUnavailableUntil = images.Min(x => x.CreatedOn).Add(_dogContext.Domain.ImagesLimit.Window);
            }
        }

        return imagesUnavailableUntil;
    }

    protected override async Task<DateTime?> GetVideosUnavailableUntil(DateTime now)
    {
        DateTime? videosUnavailableUntil = null;
        if (_dogContext.Domain.VideosLimit != null)
        {
            var videosSince = now.Add(-_dogContext.Domain.VideosLimit.Window);
            var videos = (await _dogDatabase.Interactions
                    .Where(x => x.InteractionType == InteractionType.VideosSuccess)
                    .Where(x => x.CreatedOn >= videosSince)
                    .ToListAsync())
                .Select(x => (x.CreatedOn, count: int.Parse(x.Serialized)))
                .ToList();

            if (videos.Sum(x => x.count) >= _dogContext.Domain.VideosLimit.Limit)
            {
                videosUnavailableUntil = videos.Min(x => x.CreatedOn).Add(_dogContext.Domain.VideosLimit.Window);
            }
        }

        return videosUnavailableUntil;
    }

    protected override async Task<DateTime?> GetMusicUnavailableUntil(DateTime now)
    {
        DateTime? musicUnavailableUntil = null;
        if (_dogContext.Domain.MusicLimit != null)
        {
            var musicSince = now.Add(-_dogContext.Domain.MusicLimit.Window);
            var music = (await _dogDatabase.Interactions
                    .Where(x => x.InteractionType == InteractionType.MusicSuccess)
                    .Where(x => x.CreatedOn >= musicSince)
                    .ToListAsync())
                .Select(x => (x.CreatedOn, count: int.Parse(x.Serialized)))
                .ToList();

            if (music.Sum(x => x.count) >= _dogContext.Domain.MusicLimit.Limit)
            {
                musicUnavailableUntil = music.Min(x => x.CreatedOn).Add(_dogContext.Domain.MusicLimit.Window);
            }
        }

        return musicUnavailableUntil;
    }

    protected override async Task<DateTime?> GetChatUnavailableUntil() => null;

    protected override string UnavailableUntilTemplate => throw new InvalidOperationException();

    protected override async Task<(string? system, string? version, float? frequencyPenalty, float? presencePenalty, int? imagesVersion, string? videoModel, string? musicModel)> GetAiParameters()
    {
        var domain = _dogContext.Domain;
        return (domain.SystemMessage, domain.GptVersion, domain.FrequencyPenalty, domain.PresencePenalty, domain.DalleVersion, domain.SoraModel, domain.LyriaModel);
    }
}