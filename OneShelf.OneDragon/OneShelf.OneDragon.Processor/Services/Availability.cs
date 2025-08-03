using Microsoft.EntityFrameworkCore;
using OneShelf.OneDragon.Database;
using OneShelf.OneDragon.Database.Model.Enums;

namespace OneShelf.OneDragon.Processor.Services;

public class Availability(DragonDatabase dragonDatabase, DragonScope dragonScope)
{
    public async Task<DateTime?> GetImagesUnavailableUntil(DateTime now)
    {
        var user = await dragonDatabase.Users.SingleAsync(x => x.Id == dragonScope.UserId);
        if (!user.UseLimits) return null;

        var limits = await dragonDatabase.Limits
            .Where(x => x.Images.HasValue && x.IsEnabled)
            .Where(x => x.Group == user.Group)
            .ToListAsync();
        if (!limits.Any()) return null;

        DateTime Since(TimeSpan window) => now.Add(-window);

        DateTime? imagesUnavailableUntil = null;
        foreach (var limit in limits)
        {
            // For shared limits, count all users in the group
            // For non-shared limits, count only current user
            List<(DateTime CreatedOn, int count)> images;
            var since = Since(limit.Window);

            if (limit.IsShared)
            {
                // Get all users in the same group
                var groupUserIds = await dragonDatabase.Users
                    .Where(x => x.Group == user.Group)
                    .Select(x => x.Id)
                    .ToListAsync();
                
                images = (await dragonDatabase.Interactions
                        .Where(x => groupUserIds.Contains(x.UserId))
                        .Where(x => x.InteractionType == InteractionType.AiImagesSuccess || x.InteractionType == InteractionType.DirectImagesSuccess)
                        .Where(x => x.CreatedOn >= since)
                        .ToListAsync())
                    .Select(x => (x.CreatedOn, count: int.Parse(x.Serialized)))
                    .ToList();
            }
            else
            {
                images = (await dragonDatabase.Interactions
                        .Where(x => x.UserId == dragonScope.UserId && x.ChatId == dragonScope.ChatId)
                        .Where(x => x.InteractionType == InteractionType.AiImagesSuccess || x.InteractionType == InteractionType.DirectImagesSuccess)
                        .Where(x => x.CreatedOn >= since)
                        .ToListAsync())
                    .Select(x => (x.CreatedOn, count: int.Parse(x.Serialized)))
                    .ToList();
            }

            if (images.Sum(x => x.count) >= limit.Images!.Value)
            {
                imagesUnavailableUntil ??= DateTime.MinValue;
                var value = images.Min(x => x.CreatedOn).Add(limit.Window);
                imagesUnavailableUntil = imagesUnavailableUntil > value ? imagesUnavailableUntil : value;
            }
        }

        return imagesUnavailableUntil;
    }

    public async Task<DateTime?> GetVideosUnavailableUntil(DateTime now)
    {
        var user = await dragonDatabase.Users.SingleAsync(x => x.Id == dragonScope.UserId);
        if (!user.UseLimits) return null;

        var limits = await dragonDatabase.Limits
            .Where(x => x.Videos.HasValue && x.IsEnabled)
            .Where(x => x.Group == user.Group)
            .ToListAsync();
        if (!limits.Any()) return null;

        DateTime Since(TimeSpan window) => now.Add(-window);

        DateTime? videosUnavailableUntil = null;
        foreach (var limit in limits)
        {
            // For shared limits, count all users in the group
            // For non-shared limits, count only current user
            List<(DateTime CreatedOn, int count)> videos;
            var since = Since(limit.Window);
            if (limit.IsShared)
            {
                // Get all users in the same group
                var groupUserIds = await dragonDatabase.Users
                    .Where(x => x.Group == user.Group)
                    .Select(x => x.Id)
                    .ToListAsync();
                
                videos = (await dragonDatabase.Interactions
                        .Where(x => groupUserIds.Contains(x.UserId))
                        .Where(x => x.InteractionType == InteractionType.DirectVideosSuccess || x.InteractionType == InteractionType.AiVideosSuccess)
                        .Where(x => x.CreatedOn >= since)
                        .ToListAsync())
                    .Select(x => (x.CreatedOn, count: int.Parse(x.Serialized)))
                    .ToList();
            }
            else
            {
                videos = (await dragonDatabase.Interactions
                        .Where(x => x.UserId == dragonScope.UserId && x.ChatId == dragonScope.ChatId)
                        .Where(x => x.InteractionType == InteractionType.DirectVideosSuccess || x.InteractionType == InteractionType.AiVideosSuccess)
                        .Where(x => x.CreatedOn >= since)
                        .ToListAsync())
                    .Select(x => (x.CreatedOn, count: int.Parse(x.Serialized)))
                    .ToList();
            }

            if (videos.Sum(x => x.count) >= limit.Videos!.Value)
            {
                videosUnavailableUntil ??= DateTime.MinValue;
                var value = videos.Min(x => x.CreatedOn).Add(limit.Window);
                videosUnavailableUntil = videosUnavailableUntil > value ? videosUnavailableUntil : value;
            }
        }

        return videosUnavailableUntil;
    }

    public async Task<DateTime?> GetSongsUnavailableUntil(DateTime now)
    {
        var user = await dragonDatabase.Users.SingleAsync(x => x.Id == dragonScope.UserId);
        if (!user.UseLimits) return null;

        var limits = await dragonDatabase.Limits
            .Where(x => x.Songs.HasValue && x.IsEnabled)
            .Where(x => x.Group == user.Group)
            .ToListAsync();
        if (!limits.Any()) return null;

        DateTime Since(TimeSpan window) => now.Add(-window);

        DateTime? songsUnavailableUntil = null;
        foreach (var limit in limits)
        {
            // For shared limits, count all users in the group
            // For non-shared limits, count only current user
            List<(DateTime CreatedOn, int count)> songs;
            var since = Since(limit.Window);
            if (limit.IsShared)
            {
                // Get all users in the same group
                var groupUserIds = await dragonDatabase.Users
                    .Where(x => x.Group == user.Group)
                    .Select(x => x.Id)
                    .ToListAsync();
                
                songs = (await dragonDatabase.Interactions
                        .Where(x => groupUserIds.Contains(x.UserId))
                        .Where(x => x.InteractionType == InteractionType.DirectSongsSuccess || x.InteractionType == InteractionType.AiSongsSuccess)
                        .Where(x => x.CreatedOn >= since)
                        .ToListAsync())
                    .Select(x => (x.CreatedOn, count: int.Parse(x.Serialized)))
                    .ToList();
            }
            else
            {
                songs = (await dragonDatabase.Interactions
                        .Where(x => x.UserId == dragonScope.UserId && x.ChatId == dragonScope.ChatId)
                        .Where(x => x.InteractionType == InteractionType.DirectSongsSuccess || x.InteractionType == InteractionType.AiSongsSuccess)
                        .Where(x => x.CreatedOn >= since)
                        .ToListAsync())
                    .Select(x => (x.CreatedOn, count: int.Parse(x.Serialized)))
                    .ToList();
            }

            if (songs.Sum(x => x.count) >= limit.Songs!.Value)
            {
                songsUnavailableUntil ??= DateTime.MinValue;
                var value = songs.Min(x => x.CreatedOn).Add(limit.Window);
                songsUnavailableUntil = songsUnavailableUntil > value ? songsUnavailableUntil : value;
            }
        }

        return songsUnavailableUntil;
    }
}