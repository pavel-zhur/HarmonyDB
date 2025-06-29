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

        var limits = await dragonDatabase.Limits.Where(x => x.Images.HasValue).ToListAsync();
        if (!limits.Any()) return null;

        DateTime Since(TimeSpan window) => now.Add(-window);

        var imagesSince = Since(limits.Max(x => x.Window));
        var images = (await dragonDatabase.Interactions
                .Where(x => x.UserId == dragonScope.UserId && x.ChatId == dragonScope.ChatId)
                .Where(x => x.InteractionType == InteractionType.AiImagesSuccess || x.InteractionType == InteractionType.DirectImagesSuccess)
                .Where(x => x.CreatedOn >= imagesSince)
                .ToListAsync())
            .Select(x => (x.CreatedOn, count: int.Parse(x.Serialized)))
            .ToList();

        DateTime? imagesUnavailableUntil = null;
        foreach (var limit in limits)
        {
            if (images.Where(x => x.CreatedOn >= Since(limit.Window)).Sum(x => x.count) >= limit.Images!.Value)
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

        var limits = await dragonDatabase.Limits.Where(x => x.Videos.HasValue).ToListAsync();
        if (!limits.Any()) return null;

        DateTime Since(TimeSpan window) => now.Add(-window);

        var videosSince = Since(limits.Max(x => x.Window));
        var videos = (await dragonDatabase.Interactions
                .Where(x => x.UserId == dragonScope.UserId && x.ChatId == dragonScope.ChatId)
                .Where(x => x.InteractionType == InteractionType.DirectVideosSuccess || x.InteractionType == InteractionType.AiVideosSuccess)
                .Where(x => x.CreatedOn >= videosSince)
                .ToListAsync())
            .Select(x => (x.CreatedOn, count: int.Parse(x.Serialized)))
            .ToList();

        DateTime? videosUnavailableUntil = null;
        foreach (var limit in limits)
        {
            if (videos.Where(x => x.CreatedOn >= Since(limit.Window)).Sum(x => x.count) >= limit.Videos!.Value)
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

        var limits = await dragonDatabase.Limits.Where(x => x.Songs.HasValue).ToListAsync();
        if (!limits.Any()) return null;

        DateTime Since(TimeSpan window) => now.Add(-window);

        var songsSince = Since(limits.Max(x => x.Window));
        var songs = (await dragonDatabase.Interactions
                .Where(x => x.UserId == dragonScope.UserId && x.ChatId == dragonScope.ChatId)
                .Where(x => x.InteractionType == InteractionType.DirectSongsSuccess || x.InteractionType == InteractionType.AiSongsSuccess)
                .Where(x => x.CreatedOn >= songsSince)
                .ToListAsync())
            .Select(x => (x.CreatedOn, count: int.Parse(x.Serialized)))
            .ToList();

        DateTime? songsUnavailableUntil = null;
        foreach (var limit in limits)
        {
            if (songs.Where(x => x.CreatedOn >= Since(limit.Window)).Sum(x => x.count) >= limit.Songs!.Value)
            {
                songsUnavailableUntil ??= DateTime.MinValue;
                var value = songs.Min(x => x.CreatedOn).Add(limit.Window);
                songsUnavailableUntil = songsUnavailableUntil > value ? songsUnavailableUntil : value;
            }
        }

        return songsUnavailableUntil;
    }
}