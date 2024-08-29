using CasCap.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Videos.App.Database;
using OneShelf.Videos.App.Database.Models;

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

    public async Task SaveInventory()
    {
        await _googlePhotosService.LoginAsync();
        var items = await _googlePhotosService.GetMediaItemsAsync().ToListAsync();
        _logger.LogInformation("{items} found.", items.Count);
        _videosDatabase.InventoryItems.AddRange(items.Select(i => new InventoryItem
        {
            BaseUrl = i.baseUrl,
            Description = i.description,
            FileName = i.filename,
            Id = i.id,
            IsPhoto = i.isPhoto,
            IsVideo = i.isVideo,
            ProductUrl = i.productUrl,
            SyncDate = i.syncDate,
            MimeType = i.mimeType,
            ContributorInfoDisplayName = i.contributorInfo?.displayName,
            ContributorInfoProfilePictureBaseUrl = i.contributorInfo?.profilePictureBaseUrl,
            MediaMetadataHeight = i.mediaMetadata.height,
            MediaMetadataWidth = i.mediaMetadata.width,
            MediaMetadataCreationTime = i.mediaMetadata.creationTime,
            MediaMetadataPhotoApertureFNumber = i.mediaMetadata.photo?.apertureFNumber,
            MediaMetadataPhotoExposureTime = i.mediaMetadata.photo?.exposureTime,
            MediaMetadataPhotoFocalLength = i.mediaMetadata.photo?.focalLength,
            MediaMetadataPhotoIsoEquivalent = i.mediaMetadata.photo?.isoEquivalent,
            MediaMetadataPhotoCameraMake = i.mediaMetadata.photo?.cameraMake,
            MediaMetadataPhotoCameraModel = i.mediaMetadata.photo?.cameraModel,
            MediaMetadataVideoStatus = i.mediaMetadata.video?.status,
            MediaMetadataVideoFps = i.mediaMetadata.video?.fps,
            MediaMetadataVideoCameraMake = i.mediaMetadata.video?.cameraMake,
            MediaMetadataVideoCameraModel = i.mediaMetadata.video?.cameraModel,
        }));
        _logger.LogInformation("Saving...");
        await _videosDatabase.SaveChangesAsync();
        _logger.LogInformation("Saved.");
    }

    public async Task UploadPhotos(List<(long chatId, int messageId, string path, DateTime publishedOn)> items)
    {
        await _googlePhotosService.LoginAsync();

        var itemsByKey = items.ToDictionary(x => (x.chatId, x.messageId));
        var fileNameTimestamps = new Dictionary<(long chatId, int messageId), DateTime>();
        var result = await _googlePhotosService.UploadMultiple(
            items
                .Select(x => ((x.chatId, x.messageId), x.path, (string?)null))
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
                }

                return tempFileName;
            },
            3);

        Console.WriteLine($"started: {items.Count}, finished: {result.Count}");
    }

    public async Task UploadVideos(List<(long chatId, int messageId, string path, DateTime publishedOn)> items)
    {
        await _googlePhotosService.LoginAsync();

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
            newItems => AddToDatabase(itemsByKey, newItems),
            threads: 3,
            batchSize: 10);

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