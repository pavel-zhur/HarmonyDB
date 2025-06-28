using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneShelf.Common.OpenAi.Models;
using OneShelf.Common.OpenAi.Services;
using OneShelf.OneDragon.Database;
using OneShelf.OneDragon.Database.Model.Enums;
using OneShelf.OneDragon.Processor.Model;
using OneShelf.OneDragon.Processor.Services;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;

namespace OneShelf.OneDragon.Processor.Commands;

[BothCommand("songs", "Музыка", "Сделать музыку по текстовому описанию")]
public class Songs : Command
{
    private readonly DragonDatabase _dragonDatabase;
    private readonly DragonScope _scope;
    private readonly Availability _availability;
    private readonly MusicGenerator _musicGenerator;
    private readonly TelegramBotClient _api;

    public Songs(Io io, DragonDatabase dragonDatabase, DragonScope scope, Availability availability, MusicGenerator musicGenerator, IOptions<TelegramOptions> options) 
        : base(io)
    {
        _dragonDatabase = dragonDatabase;
        _scope = scope;
        _availability = availability;
        _musicGenerator = musicGenerator;
        _api = new(options.Value.Token);
    }

    protected override async Task ExecuteQuickly()
    {
        var songsUnavailableUntil = await _availability.GetSongsUnavailableUntil(DateTime.Now);
        if (songsUnavailableUntil != null)
        {
            Io.WriteLine($"Лапки устали сочинять! Музыка будет доступна с {songsUnavailableUntil.Value:g} UTC 🐾");

            _dragonDatabase.Interactions.Add(new()
            {
                CreatedOn = DateTime.Now,
                ChatId = _scope.ChatId,
                UserId = Io.UserId,
                UpdateId = _scope.UpdateId,
                InteractionType = InteractionType.DirectSongsLimit,
                Serialized = "reset",
            });

            return;
        }

        var query = Io.FreeChoice("Какую музыку хочешь? (описание на английском)");
        
        var useNegativePrompt = Io.StrictChoice("Исключить что-то из музыки?", bool.Parse, new[] { "true", "false" });
        
        string? negativePrompt = null;
        if (useNegativePrompt)
        {
            negativePrompt = Io.FreeChoice("Что НЕ должно быть в музыке (на английском):");
        }

        Io.WriteLine("Сочиняю 30-секундную музыку! 🎵");

        await _api.SendChatActionAsync(_scope.ChatId, ChatActions.UploadVoice);

        Scheduled(Background(query, negativePrompt));
    }

    private async Task Background(string query, string? negativePrompt)
    {
        var aiParameters = await _dragonDatabase.AiParameters.SingleAsync();


        var result = await _musicGenerator.GenerateMusic(new()
        {
            Prompt = query,
            NegativePrompt = negativePrompt,
            UserId = _scope.UserId,
            DomainId = -1,
            ChatId = _scope.ChatId,
            UseCase = "direct songs",
            AdditionalBillingInfo = "one dragon",
            Model = aiParameters.LyriaModel,
        });

        if (result.Success)
        {
            _dragonDatabase.Interactions.Add(new()
            {
                CreatedOn = DateTime.Now,
                ChatId = _scope.ChatId,
                UserId = Io.UserId,
                UpdateId = _scope.UpdateId,
                InteractionType = InteractionType.DirectSongsSuccess,
                Serialized = "1",
                ShortInfoSerialized = query,
            });

            await _dragonDatabase.SaveChangesAsync();

            // Send as audio only
            using var audioStream = new MemoryStream(result.AudioData);
            var audioFile = new InputFile(audioStream, "music.wav");
            await _api.SendAudioAsync(new(_scope.ChatId, audioFile)
            {
                Caption = $"🎵 {query} (от Lyria 🐶)"
            });
        }
        else
        {
            _dragonDatabase.Interactions.Add(new()
            {
                CreatedOn = DateTime.Now,
                ChatId = _scope.ChatId,
                UserId = Io.UserId,
                UpdateId = _scope.UpdateId,
                InteractionType = InteractionType.DirectSongsLimit,
                Serialized = "error",
                ShortInfoSerialized = result.ErrorMessage ?? "Unknown error",
            });

            await _dragonDatabase.SaveChangesAsync();

            throw new InvalidOperationException($"Music generation failed: {result.ErrorMessage}");
        }
    }
}