using CasCap.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Videos.Database;
using OneShelf.Videos.Database.Models;
using System.Globalization;

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

        var existingItems = await _videosDatabase.InventoryItems.ToDictionaryAsync(x => x.Id);

        foreach (var i in items)
        {
            var item = existingItems.GetValueOrDefault(i.id);
            if (item == null)
            {
                item = new()
                {
                    Id = i.id,
                    CreatedOn = DateTime.UtcNow,
                };

                _videosDatabase.InventoryItems.Add(item);
            }

            item.BaseUrl = i.baseUrl;
            item.Description = i.description;
            item.FileName = i.filename;
            item.IsPhoto = i.isPhoto;
            item.IsVideo = i.isVideo;
            item.ProductUrl = i.productUrl;
            item.SyncDate = i.syncDate;
            item.MimeType = i.mimeType;
            item.ContributorInfoDisplayName = i.contributorInfo?.displayName;
            item.ContributorInfoProfilePictureBaseUrl = i.contributorInfo?.profilePictureBaseUrl;
            item.MediaMetadataHeight = i.mediaMetadata.height;
            item.MediaMetadataWidth = i.mediaMetadata.width;
            item.MediaMetadataCreationTime = i.mediaMetadata.creationTime;
            item.MediaMetadataPhotoApertureFNumber = i.mediaMetadata.photo?.apertureFNumber;
            item.MediaMetadataPhotoExposureTime = i.mediaMetadata.photo?.exposureTime;
            item.MediaMetadataPhotoFocalLength = i.mediaMetadata.photo?.focalLength;
            item.MediaMetadataPhotoIsoEquivalent = i.mediaMetadata.photo?.isoEquivalent;
            item.MediaMetadataPhotoCameraMake = i.mediaMetadata.photo?.cameraMake;
            item.MediaMetadataPhotoCameraModel = i.mediaMetadata.photo?.cameraModel;
            item.MediaMetadataVideoStatus = i.mediaMetadata.video?.status;
            item.MediaMetadataVideoFps = i.mediaMetadata.video?.fps;
            item.MediaMetadataVideoCameraMake = i.mediaMetadata.video?.cameraMake;
            item.MediaMetadataVideoCameraModel = i.mediaMetadata.video?.cameraModel;
        }

        _logger.LogInformation("Saving...");
        await _videosDatabase.SaveChangesAsync();
        _logger.LogInformation("Saved.");
    }

    public async Task UploadPhotos(List<(int mediaId, string path, DateTime? exifTimestamp)> items)
    {
        await _extendedGooglePhotosService.LoginAsync();

        var itemsByKey = items.ToDictionary(x => x.mediaId);
        var result = await _extendedGooglePhotosService.UploadMultiple(
            items
                .Select(x => (x.mediaId, x.path, (string?)null))
                .ToList(),
            newItems => AddToDatabase(itemsByKey, newItems),
            async (x, i) =>
            {
                var tempDirectory = Path.Combine(Path.GetTempPath(), i.ToString());
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }

                Directory.CreateDirectory(tempDirectory);
                var fileName = itemsByKey[x].path;
                var tempFileName = Path.Combine(Path.GetTempPath(), i.ToString(), Path.GetFileName(fileName));
                var exifTimestamp = itemsByKey[x].exifTimestamp!.Value;
                await _exifService.SetExifTimestamp(fileName, tempFileName, exifTimestamp);

                return tempFileName;
            },
            3);

        Console.WriteLine($"started: {items.Count}, finished: {result.Count}");
    }

    public async Task UploadVideos(List<(int mediaId, string path, DateTime? exifTimestamp)> items)
    {
        await _extendedGooglePhotosService.LoginAsync();

        var added = (await _videosDatabase.UploadedItems.Select(x => x.MediaId).ToListAsync()).ToHashSet();
        Console.WriteLine($"initial items: {items.Count}");
        items = items.Where(x => !added.Contains(x.mediaId)).ToList();
        Console.WriteLine($"remaining items: {items.Count}");

        var itemsByKey = items.ToDictionary(x => x.mediaId);
        var result = await _extendedGooglePhotosService.UploadMultiple(
            items
                .Select(x => (x.mediaId, x.path,
                    //$"chatId = {x.chatId}, messageId = {x.messageId}, published on = {x.publishedOn}, filename = {Path.GetFileName(x.path)}"
                    (string?)null))
                .ToList(),
            newItems => AddToDatabase(itemsByKey, newItems),
            threads: 3,
            batchSize: 10);

        Console.WriteLine($"started: {items.Count}, finished: {result.Count}");
    }

    private async Task AddToDatabase(
        Dictionary<int, (int mediaId, string path, DateTime? exifTimestamp)> items,
        Dictionary<int, NewMediaItemResult> newItems)
    {
        _videosDatabaseOperations.AddUploadedItems(newItems.Select(i => items[i.Key].SelectSingle(x => (x.mediaId, x.path, result: i.Value))));
        await _videosDatabase.SaveChangesAsync();
    }

    public async Task CreateAlbums(List<(int albumId, string title, List<(string mediaItemId, int mediaId)> items)> albums)
    {
        await _extendedGooglePhotosService.LoginAsync();
        foreach (var (albumId, title, items) in albums)
        {
            _logger.LogInformation("{title} uploading...", title);
            var googleAlbum = await _extendedGooglePhotosService.CreateAlbumAsync(title);
            await _extendedGooglePhotosService.AddMediaItemsToAlbumWithRetryAsync(googleAlbum!.id, items.Select(x => x.mediaItemId).ToList());

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