using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;
using OneShelf.Videos.BusinessLogic.Models;
using OneShelf.Videos.Database;
using OneShelf.Videos.Database.Models;
using OneShelf.Videos.Telegram.Processor.Model;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.UpdatingMessages;
using File = System.IO.File;

namespace OneShelf.Videos.Telegram.Processor.Commands;

[AdminCommand("download_all", "Скачать", "Скачать с хендлерами")]
public class DownloadAll : Command
{
    private readonly VideosDatabase _videosDatabase;
    private readonly IOptions<TelegramOptions> _telegramOptions;
    private readonly IOptions<VideosOptions> _videoOptions;
    private readonly TelegramBotClient _botClient;
    private readonly HttpClient _httpClient;

    public DownloadAll(Io io, VideosDatabase videosDatabase, IOptions<TelegramOptions> telegramOptions, HttpClient httpClient, IOptions<VideosOptions> videoOptions) 
        : base(io)
    {
        _videosDatabase = videosDatabase;
        _telegramOptions = telegramOptions;
        _httpClient = httpClient;
        _videoOptions = videoOptions;
        _botClient = new(_telegramOptions.Value.Token);
    }

    protected override async Task ExecuteQuickly()
    {
        var items = await _videosDatabase.TelegramMedia
            .Where(x => x.HandlerMessageId.HasValue && x.DownloadedFileName == null).ToListAsync();

        if (!items.Any())
        {
            Io.WriteLine("Нечего скачивать.");
            return;
        }

        Scheduled(Background(items));
    }

    private async Task Background(List<TelegramMedia> items)
    {
        var progress = await _botClient.SendMessageAsync(Io.UserId, items.Count.ToString());
        var progressUpdated = DateTime.Now;

        foreach (var (telegramMedia, i) in items.WithIndices())
        {
            telegramMedia.DownloadedFileName = await Save(telegramMedia, false);
            await _videosDatabase.SaveChangesAsync();

            if (telegramMedia.ThumbnailFileId != null)
            {
                telegramMedia.DownloadedThumbnailFileName = await Save(telegramMedia, true);
                await _videosDatabase.SaveChangesAsync();
            }

            if ((DateTime.Now - progressUpdated).TotalSeconds > 5 || i == items.Count - 1)
            {
                await _botClient.EditMessageTextAsync(progress.Chat.Id, progress.MessageId, $"{i + 1}/{items.Count}");
                progressUpdated = DateTime.Now;
            }
        }
    }

    [Obsolete("This method doesn't work. There's a 20 MB limit for bots.")]
    private async Task<string> Save(TelegramMedia telegramMedia, bool isThumbnail)
    {
        var file = await _botClient.GetFileAsync(isThumbnail ? telegramMedia.ThumbnailFileId! : telegramMedia.FileId);
        Console.WriteLine(file.FilePath);
        var response = await _httpClient.GetAsync($"https://api.telegram.org/file/bot{_telegramOptions.Value.Token}/{file.FilePath}");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Console.WriteLine(bytes.Length);
        var name = $"{DateTime.Now.Ticks}_{telegramMedia.FileName}";
        await File.WriteAllBytesAsync(Path.Combine(_videoOptions.Value.BasePath, "_uploaded", isThumbnail ? "thumbnails" : ".", name), bytes);
        return name;
    }
}