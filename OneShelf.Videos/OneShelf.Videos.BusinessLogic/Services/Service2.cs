using CasCap.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Videos.Database;
using OneShelf.Videos.Database.Models;

namespace OneShelf.Videos.BusinessLogic.Services;

public class Service2
{
    private readonly ExtendedGooglePhotosService _extendedGooglePhotosService;
    private readonly ILogger<Service2> _logger;
    private readonly VideosDatabase _videosDatabase;
    private readonly VideosDatabaseOperations _videosDatabaseOperations;
    private readonly ExifService _exifService;

    public Service2(ExtendedGooglePhotosService extendedGooglePhotosService, ILogger<Service2> logger, VideosDatabase videosDatabase, ExifService exifService, VideosDatabaseOperations videosDatabaseOperations)
    {
        _extendedGooglePhotosService = extendedGooglePhotosService;
        _logger = logger;
        _videosDatabase = videosDatabase;
        _exifService = exifService;
        _videosDatabaseOperations = videosDatabaseOperations;
    }

    public async Task<List<string>> ListAlbums()
    {
        await _extendedGooglePhotosService.LoginAsync();
        var albums = await _extendedGooglePhotosService.GetAlbumsAsync();
        return albums.Select(x => x.title).ToList();
    }

    public async Task SaveInventory()
    {
        await _extendedGooglePhotosService.LoginAsync();
        var items = await _extendedGooglePhotosService.GetMediaItemsAsync().ToListAsync();
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
        await _extendedGooglePhotosService.LoginAsync();

        var itemsByKey = items.ToDictionary(x => (x.chatId, x.messageId));
        var fileNameTimestamps = new Dictionary<(long chatId, int messageId), DateTime>();
        var result = await _extendedGooglePhotosService.UploadMultiple(
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
                var timestampFromFile = await _exifService.SetExifTimestamp(itemsByKey[x].path, tempFileName);
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
        await _extendedGooglePhotosService.LoginAsync();

        var added = (await _videosDatabase.UploadedItems.Select(x => new { ChatId = x.StaticChatId, MessageId = x.StaticMessageId }).ToListAsync()).ToHashSet();
        Console.WriteLine($"initial items: {items.Count}");
        items = items.Where(x => !added.Contains(new { ChatId = x.chatId, MessageId = x.messageId })).ToList();
        Console.WriteLine($"remaining items: {items.Count}");

        var itemsByKey = items.ToDictionary(x => (x.chatId, x.messageId));
        var result = await _extendedGooglePhotosService.UploadMultiple(
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
        _videosDatabaseOperations.AddItems(newItems.Select(i => items[i.Key].SelectSingle(x => (x.chatId, x.messageId, x.path, x.publishedOn, result: i.Value, fileNameTimestamp: fileNameTimestamps?[i.Key]))));
        await _videosDatabase.SaveChangesAsync();
    }

    public async Task CreateAlbums(List<(int albumId, string title, List<(string? mediaItemId, long chatId, int messageId)> items)> albums)
    {
        await _extendedGooglePhotosService.LoginAsync();
        foreach (var (albumId, title, items) in albums)
        {
            _logger.LogInformation("{title} uploading...", title);
            var googleAlbum = await _extendedGooglePhotosService.CreateAlbumAsync(title);
            await _extendedGooglePhotosService.AddMediaItemsToAlbumWithRetryAsync(googleAlbum!.id, items.Where(x => x.mediaItemId != null).Select(x => x.mediaItemId!).ToList());

            _videosDatabase.UploadedAlbums.Add(new()
            {
                GoogleAlbumId = googleAlbum.id,
                AlbumId = albumId,
            });

            await _videosDatabase.SaveChangesAsync();
            _logger.LogInformation("{title} uploaded.", title);
        }
    }
}