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

[BothCommand("videos", "Видео", "Сделать видео по текстовому описанию")]
public class Videos : Command
{
    private readonly DragonDatabase _dragonDatabase;
    private readonly DragonScope _scope;
    private readonly Availability _availability;
    private readonly VideoGenerator _videoGenerator;
    private readonly TelegramBotClient _api;

    public Videos(Io io, DragonDatabase dragonDatabase, DragonScope scope, Availability availability, VideoGenerator videoGenerator, IOptions<TelegramOptions> options) 
        : base(io)
    {
        _dragonDatabase = dragonDatabase;
        _scope = scope;
        _availability = availability;
        _videoGenerator = videoGenerator;
        _api = new(options.Value.Token);
    }

    protected override async Task ExecuteQuickly()
    {
        var videosUnavailableUntil = await _availability.GetVideosUnavailableUntil(DateTime.Now);
        if (videosUnavailableUntil != null)
        {
            Io.WriteLine($"Лапки устали снимать! Видео будет доступно с {videosUnavailableUntil.Value:g} UTC 🐾");

            _dragonDatabase.Interactions.Add(new()
            {
                CreatedOn = DateTime.Now,
                ChatId = _scope.ChatId,
                UserId = Io.UserId,
                UpdateId = _scope.UpdateId,
                InteractionType = InteractionType.DirectVideosLimit,
                Serialized = "reset",
            });

            return;
        }

        var query = Io.FreeChoice("Какое видео хочешь?");
        
        var resolutionChoice = Io.StrictChoice("Какой формат?", x => x, new[] { "16:9", "9:16", "Square" });
        
        var duration = Io.StrictChoice("Сколько секунд? (1-20)", int.Parse, new[] { "5", "10", "15", "20" });

        if (duration is not (>= 1 and <= 20))
        {
            Io.WriteLine("Длительность видео должна быть от 1 до 20 секунд.");
            return;
        }

        Io.WriteLine("Генерирую видео с помощью Sora! 🎬");

        Scheduled(Background(query, resolutionChoice, duration));
    }

    private async Task Background(string query, string resolutionChoice, int duration)
    {
        var aiParameters = await _dragonDatabase.AiParameters.SingleAsync();

        // Convert resolution choice to width/height
        var (width, height) = resolutionChoice switch
        {
            "16:9" => (1280, 720),
            "9:16" => (720, 1280),
            "Square" => (720, 720),
            _ => (720, 720)
        };

        var result = await _videoGenerator.GenerateVideo(new()
        {
            Prompt = query,
            Width = width,
            Height = height,
            Duration = duration,
            UserId = _scope.UserId,
            DomainId = -1,
            ChatId = _scope.ChatId,
            UseCase = "direct videos",
            AdditionalBillingInfo = "one dragon",
            Model = aiParameters.SoraModel,
        });

        if (result.Success)
        {
            _dragonDatabase.Interactions.Add(new()
            {
                CreatedOn = DateTime.Now,
                ChatId = _scope.ChatId,
                UserId = Io.UserId,
                UpdateId = _scope.UpdateId,
                InteractionType = InteractionType.DirectVideosSuccess,
                Serialized = "1",
                ShortInfoSerialized = query,
            });

            await _dragonDatabase.SaveChangesAsync();

            // Send as video (Sora videos from Azure OpenAI)
            using var videoStream = new MemoryStream(result.VideoData);
            var videoFile = new InputFile(videoStream, "video.mp4");
            
            await _api.SendVideoAsync(new(_scope.ChatId, videoFile) 
            { 
                Caption = $"🎬 {query} (от Sora 🐶)" 
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
                InteractionType = InteractionType.DirectVideosLimit,
                Serialized = "error",
                ShortInfoSerialized = result.ErrorMessage ?? "Unknown error",
            });

            await _dragonDatabase.SaveChangesAsync();

            throw new InvalidOperationException($"Video generation failed: {result.ErrorMessage}");
        }
    }
}