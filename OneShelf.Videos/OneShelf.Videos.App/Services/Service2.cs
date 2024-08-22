using CasCap.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Videos.App.Database;

namespace OneShelf.Videos.App.Services;

public class Service2
{
    private readonly UpdatedGooglePhotosService.UpdatedGooglePhotosService _googlePhotosService;
    private readonly ILogger<Service2> _logger;
    private readonly VideosDatabase _videosDatabase;
    private readonly Service3 _service3;

    public Service2(UpdatedGooglePhotosService.UpdatedGooglePhotosService googlePhotosService, ILogger<Service2> logger, VideosDatabase videosDatabase, Service3 service3)
    {
        _googlePhotosService = googlePhotosService;
        _logger = logger;
        _videosDatabase = videosDatabase;
        _service3 = service3;
    }

    public async Task ListAlbums()
    {
        _googlePhotosService.LoginWithOptions();
        var albums = await _googlePhotosService.GetAlbumsAsync();
        foreach (var album in albums)
        {
            _logger.LogInformation($"{album.id}\t{album.title}");
        }
    }

    public async Task UploadPhotos(List<(long chatId, int messageId, string path, DateTime publishedOn)> items)
    {
        _googlePhotosService.LoginWithOptions();

        var added = (await _videosDatabase.UploadedItems.Select(x => new { x.ChatId, x.MessageId }).ToListAsync()).ToHashSet();
        Console.WriteLine($"initial items: {items.Count}");
        items = items.Where(x => !added.Contains(new { ChatId = x.chatId, MessageId = x.messageId })).ToList();
        Console.WriteLine($"remaining items: {items.Count}");

        var itemsByKey = items.ToDictionary(x => (x.chatId, x.messageId));
        var fileNameTimestamps = new Dictionary<(long chatId, int messageId), DateTime>();
        var result = await _googlePhotosService.UploadMultiple(
            items
                .Select(x => ((x.chatId, x.messageId), x.path,
                    //$"chatId = {x.chatId}, messageId = {x.messageId}, published on = {x.publishedOn}, filename = {Path.GetFileName(x.path)}"
                    (string?)null))
                .ToList(),
            newItems => AddToDatabase(itemsByKey, newItems, fileNameTimestamps),
            async (x, i) =>
            {
                var tempDirectory = Path.Combine(Path.GetTempPath(), i.ToString());
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }

                Directory.CreateDirectory(tempDirectory);
                var tempFileName = Path.Combine(Path.GetTempPath(), i.ToString(), Path.GetFileName(itemsByKey[x].path));
                var timestampFromFile = await _service3.SetExifTimestamp(itemsByKey[x].path, tempFileName);
                lock (fileNameTimestamps)
                {
                    fileNameTimestamps[x] = timestampFromFile;
                    Console.WriteLine(timestampFromFile);
                    Console.WriteLine(x);
                }

                return tempFileName;
            });

        Console.WriteLine($"started: {items.Count}, finished: {result.Count}");
    }

    public async Task UploadVideos(List<(long chatId, int messageId, string path, DateTime publishedOn)> items)
    {
        _googlePhotosService.LoginWithOptions();

        var added = (await _videosDatabase.UploadedItems.Select(x => new { x.ChatId, x.MessageId }).ToListAsync()).ToHashSet();
        Console.WriteLine($"initial items: {items.Count}");
        items = items.Where(x => !added.Contains(new { ChatId = x.chatId, MessageId = x.messageId })).ToList();
        Console.WriteLine($"remaining items: {items.Count}");

        var itemsByKey = items.ToDictionary(x => (x.chatId, x.messageId));
        var result = await _googlePhotosService.UploadMultiple(
            items
                .Select(x => ((x.chatId, x.messageId), x.path,
                    //$"chatId = {x.chatId}, messageId = {x.messageId}, published on = {x.publishedOn}, filename = {Path.GetFileName(x.path)}"
                    (string?)null))
                .ToList(),
            newItems => AddToDatabase(itemsByKey, newItems));

        Console.WriteLine($"started: {items.Count}, finished: {result.Count}");
    }

    private async Task AddToDatabase(
        Dictionary<(long chatId, int messageId), (long chatId, int messageId, string path, DateTime publishedOn)> items,
        Dictionary<(long chatId, int messageId), NewMediaItemResult> newItems,
        Dictionary<(long chatId, int messageId), DateTime>? fileNameTimestamps = null)
    {
        _videosDatabase.AddItems(newItems.Select(i => items[i.Key].SelectSingle(x => (x.chatId, x.messageId, x.path, x.publishedOn, result: i.Value, fileNameTimestamp: fileNameTimestamps?[i.Key]))));
        await _videosDatabase.SaveChangesAsync();
    }
}