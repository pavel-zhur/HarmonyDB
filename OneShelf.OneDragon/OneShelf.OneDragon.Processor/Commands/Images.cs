using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

[BothCommand("images", "Картинки", "Сделать картинки по текстовому описанию")]
public class Images : Command
{
    private readonly DragonDatabase _dragonDatabase;
    private readonly DragonScope _scope;
    private readonly Availability _availability;
    private readonly DialogRunner _dialogRunner;
    private readonly TelegramBotClient _api;

    public Images(Io io, DragonDatabase dragonDatabase, DragonScope scope, Availability availability, DialogRunner dialogRunner, IOptions<TelegramOptions> options) 
        : base(io)
    {
        _dragonDatabase = dragonDatabase;
        _scope = scope;
        _availability = availability;
        _dialogRunner = dialogRunner;
        _api = new(options.Value.Token);
    }

    protected override async Task ExecuteQuickly()
    {
        var imagesUnavailableUntil = await _availability.GetImagesUnavailableUntil(DateTime.Now);
        if (imagesUnavailableUntil != null)
        {
            Io.WriteLine($"Картинок нет до {imagesUnavailableUntil.Value:g} UTC");

            _dragonDatabase.Interactions.Add(new()
            {
                CreatedOn = DateTime.Now,
                ChatId = _scope.ChatId,
                UserId = Io.UserId,
                UpdateId = _scope.UpdateId,
                InteractionType = InteractionType.DirectImagesLimit,
                Serialized = "reset",
            });

            return;
        }

        var query = Io.FreeChoice("Подробное описание:");
        var count = Io.StrictChoice("Сколько?", int.Parse, new[] { "1", "2", "3", "4", "5" });

        if (count is not (>= 1 and <= 5))
        {
            Io.WriteLine("Многовато или маловато.");
            return;
        }

        Io.WriteLine("Рисую...");

        Scheduled(Background(query, count));
    }

    private async Task Background(string query, int count)
    {
        var aiParameters = await _dragonDatabase.AiParameters.SingleAsync();
        var images = await _dialogRunner.GenerateImages(Enumerable.Repeat(query, count).ToList(), new()
        {
            ImagesVersion = aiParameters.DalleVersion,
            UserId = _scope.UserId,
            DomainId = -1,
            Version = aiParameters.GptVersion,
            ChatId = _scope.ChatId,
            UseCase = "direct images",
            AdditionalBillingInfo = "one dragon",
            SystemMessage = "no message",
        });

        _dragonDatabase.Interactions.Add(new()
        {
            CreatedOn = DateTime.Now,
            ChatId = _scope.ChatId,
            UserId = Io.UserId,
            UpdateId = _scope.UpdateId,
            InteractionType = InteractionType.DirectImagesSuccess,
            Serialized = count.ToString(),
            ShortInfoSerialized = query,
        });

        await _dragonDatabase.SaveChangesAsync();

        await _api.SendMediaGroupAsync(new(_scope.ChatId, images.Select(x => new InputMediaPhoto(x.ToString()) {Caption = "1"})));
    }
}