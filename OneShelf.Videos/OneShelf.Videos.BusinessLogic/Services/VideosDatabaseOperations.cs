﻿using System.Text.Json;
using CasCap.Models;
using OneShelf.Videos.Database;
using OneShelf.Videos.Database.Models;

namespace OneShelf.Videos.BusinessLogic.Services;

public class VideosDatabaseOperations(VideosDatabase videosDatabase)
{
    public void AddUploadedItems(IEnumerable<(int mediaId, string path, NewMediaItemResult result)> items)
    {
        videosDatabase.UploadedItems.AddRange(items.Select(i => new UploadedItem
        {
            CreatedOn = DateTime.Now,
            MediaId = i.mediaId,
            Status = i.result.status.status,
            StatusCode = i.result.status.code,
            StatusMessage = i.result.status.message,
            MediaItemId = i.result.mediaItem?.id,
            MediaItemIsPhoto = i.result.mediaItem?.isPhoto,
            MediaItemIsVideo = i.result.mediaItem?.isVideo,
            MediaItemMimeType = i.result.mediaItem?.mimeType,
            MediaItemSyncDate = i.result.mediaItem?.syncDate,
            MediaItemMetadataCreationTime = i.result.mediaItem?.mediaMetadata?.creationTime,
            Json = JsonSerializer.Serialize(i.result.mediaItem),
        }));
    }
}