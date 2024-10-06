using System.Text.RegularExpressions;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Index.Api.Model.VInternal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.Database.Songs.Services;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.GettingUpdates;
using Constants = OneShelf.Telegram.Helpers.Constants;
using TelegramOptions = OneShelf.Telegram.Processor.Model.TelegramOptions;

namespace OneShelf.Telegram.Processor.Services.PipelineHandlers;

public class PublicImportHandler : PipelineHandler
{
    private readonly ILogger<PublicImportHandler> _logger;
    private readonly TelegramOptions _telegramOptions;
    private readonly SongsOperations _songsOperations;
    private readonly TelegramBotClient _botClient;
    private readonly RegenerationQueue _registrationQueue;
    private readonly IndexApiClient _indexApiClient;
    private readonly SongsDatabase _songsDatabase;

    public PublicImportHandler(
        ILogger<PublicImportHandler> logger,
        IOptions<TelegramOptions> telegramOptions,
        SongsOperations songsOperations, 
        RegenerationQueue registrationQueue, 
        IndexApiClient indexApiClient, 
        IScopedAbstractions scopedAbstractions, SongsDatabase songsDatabase)
        : base(scopedAbstractions)
    {
        _logger = logger;
        _telegramOptions = telegramOptions.Value;
        _songsOperations = songsOperations;
        _registrationQueue = registrationQueue;
        _indexApiClient = indexApiClient;
        _songsDatabase = songsDatabase;
        _botClient = new(telegramOptions.Value.Token);
    }

    private bool CheckOurChat(Update update)
    {
        if (update.Message?.Chat.Username != _telegramOptions.PublicLibraryChatId.Substring(1)
            && update.Message?.Chat.Username != _telegramOptions.PublicDogChatId.Substring(1)) return false;
        if (update.Message.From == null) return false;

        return true;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        if (!CheckOurChat(update)) return false;

        var userId = update.Message?.From?.Id;
        if (userId == null) return false;

        var text = update.Message?.Text;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var matches = Regex.Matches(text, "https://([\\w\\-]+\\.)+[\\w\\-]+(/[\\w\\- \\./\\?\\%\\&\\=]+)?");
        if (matches.Count != 1) return false;

        TryImportResponse? result = null;
        var url = matches[0].Value;

        try
        {
            var existing = await _songsDatabase.Versions
                .Where(x => x.Song.Status == SongStatus.Live)
                .Where(x => x.Song.TenantId == _telegramOptions.TenantId)
                .Include(x => x.Song)
                .ThenInclude(x => x.Artists)
                .Where(x => x.Uri == new Uri(url))
                .ToListAsync();

            if (existing.Any())
            {
                await Respond(update, string.Join(Environment.NewLine, "Уже есть в библиотеке:"
                    .Once()
                    .Append(string.Empty)
                    .Concat(existing.Select(x => $"{x.Song.Index:000}. {string.Join(", ", x.Song.Artists.Select(a => a.Name))} - {x.Song.Title}"))));
                return false;
            }

            result = await _indexApiClient.TryImport(url);
            if (string.IsNullOrWhiteSpace(result.Data?.Artist) || string.IsNullOrWhiteSpace(result.Data?.Title))
            {
                throw new("Could not get the header.");
            }

            var (songExisted, versionExisted, song) = await _songsOperations.VersionImport(_telegramOptions.TenantId, new(url), result.Data.Artist.Once().ToList(), result.Data.Title,
                userId.Value, 1);

            _registrationQueue.QueueUpdateAll(false);

            if (versionExisted || songExisted)
            {
                await Respond(update, string.Join(Environment.NewLine, versionExisted ? "Аккорды уже есть в библиотеке" : "Песня уже была в библиотеке, новые аккорды добавлены."
                    .Once()
                    .Append(string.Empty)
                    .Append($"{song.Index:000}. {string.Join(", ", song.Artists.Select(a => a.Name))} - {song.Title}")));
                return true;
            }

            await Respond(update, $"Добавлена в библиотеку: {song.Index:000}. {result.Data.Artist} - {result.Data.Title}");
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not import the url {url}.", url);

            if (result?.Data?.Source != null || _telegramOptions.ReactToUrls.Any(url.Contains))
            {
                await Respond(update, "Не получилось добавить :( Что-то пошло не так. Попробуйте через шкаф, пожалуйста.");
            }

            return false;
        }
    }

    private async Task Respond(Update update, Markdown markdown)
    {
        await _botClient.SendMessageAsync(
            update.Message!.Chat.Id,
            markdown.ToString(),
            messageThreadId: update.Message.MessageThreadId,
            parseMode: Constants.MarkdownV2,
            disableNotification: true,
            replyParameters: new()
            {
                MessageId = update.Message.MessageId,
            });
    }
}