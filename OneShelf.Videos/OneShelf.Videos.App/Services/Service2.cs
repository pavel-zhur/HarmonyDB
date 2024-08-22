using CasCap.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Videos.App.Database;

namespace OneShelf.Videos.App.Services;

public class Service2
{
    private readonly UpdatedGooglePhotosService.UpdatedGooglePhotosService _googlePhotosService;
    private readonly ILogger<Service2> _logger;
    private readonly IServiceProvider _serviceProvider;

    public Service2(UpdatedGooglePhotosService.UpdatedGooglePhotosService googlePhotosService, ILogger<Service2> logger, IServiceProvider serviceProvider)
    {
        _googlePhotosService = googlePhotosService;
        _logger = logger;
        _serviceProvider = serviceProvider;
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

    public async Task UploadItems(List<(long chatId, int messageId, string path, DateTime publishedOn)> items)
    {
        _googlePhotosService.LoginWithOptions();

        var added = (await ExecuteDatabase(async db => await db.UploadedItems.Select(x => new { x.ChatId, x.MessageId }).ToListAsync())).ToHashSet();
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

    private async Task AddToDatabase(Dictionary<(long chatId, int messageId), (long chatId, int messageId, string path, DateTime publishedOn)> items, Dictionary<(long chatId, int messageId), NewMediaItemResult> newItems)
    {
        await ExecuteDatabase(async db =>
        {
            db.AddItems(newItems.Select(i => items[i.Key].SelectSingle(x => (x.chatId, x.messageId, x.path, x.publishedOn, result: i.Value))));
            await db.SaveChangesAsync();
        });
    }

    private async Task ExecuteDatabase(Func<VideosDatabase, Task> action)
    {
        await using var videosDatabase = _serviceProvider.GetRequiredService<VideosDatabase>();
        await action(videosDatabase);
    }

    private async Task<T> ExecuteDatabase<T>(Func<VideosDatabase, Task<T>> action)
    {
        await using var videosDatabase = _serviceProvider.GetRequiredService<VideosDatabase>();
        return await action(videosDatabase);
    }
}