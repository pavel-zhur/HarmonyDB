using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Videos.BusinessLogic.Models;
using OneShelf.Videos.Database;
using OneShelf.Videos.Database.Models.Enums;
using OneShelf.Videos.Database.Models.Json;

namespace OneShelf.Videos.BusinessLogic.Services;

public class Service1
{
    private readonly IOptions<VideosOptions> _options;
    private readonly VideosDatabase _videosDatabase;
    private readonly ILogger<Service1> _logger;

    public Service1(IOptions<VideosOptions> options, VideosDatabase videosDatabase, ILogger<Service1> logger)
    {
        _options = options;
        _videosDatabase = videosDatabase;
        _logger = logger;
    }

    public async Task<List<(long chatId, int messageId, string path, DateTime publishedOn)>> GetExport1()
    {
        var messages = await _videosDatabase.Messages
            .Where(x => x.SelectedType == MessageSelectedType.Photo)
            .Include(x => x.Chat)
            .ThenInclude(x => x.ChatFolder)
            .Where(x => !_videosDatabase.UploadedItems.Any(y => y.ChatId == x.ChatId && y.MessageId == x.Id))
            .ToListAsync();

        return messages
            .Select(x => (x.Chat.Id, x.Id, Path.Combine(x.Chat.ChatFolder.Root, x.Photo!), x.Date))
            .ToList();
    }

    public async Task<List<(long chatId, int messageId, string path, DateTime publishedOn)>> GetExport2()
    {
        var messages = await _videosDatabase.Messages
            .Where(x => x.SelectedType == MessageSelectedType.Video)
            .Include(x => x.Chat)
            .ThenInclude(x => x.ChatFolder)
            .Where(x => !_videosDatabase.UploadedItems.Any(y => y.ChatId == x.ChatId && y.MessageId == x.Id))
            .ToListAsync();

        return messages
            .Select(x => (x.Chat.Id, x.Id, Path.Combine(x.Chat.ChatFolder.Root, x.File!), x.Date))
            .ToList();
    }

    public async Task SaveChatFolders()
    {
        var existing = await _videosDatabase.ChatFolders.ToListAsync();
        foreach (var directory in Directory.GetDirectories(_options.Value.BasePath, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(directory);
            if (existing.Any(x => x.Name == name)) continue;

            if (!File.Exists(Path.Combine(directory, "result.json"))) continue;

            _videosDatabase.ChatFolders.Add(new()
            {
                Name = name,
                Root = directory,
            });

            _logger.LogInformation("Added {root}.", directory);
        }

        await _videosDatabase.SaveChangesAsync();
    }

    public async Task SaveMessages()
    {
        foreach (var (chatFolder, i) in (await _videosDatabase.ChatFolders.Where(f => f.Chat == null).ToListAsync()).WithIndices())
        {
            var result = JsonSerializer.Deserialize<Chat>(await File.ReadAllTextAsync(chatFolder.ResultJsonFullPath), new JsonSerializerOptions
            {
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            })!;

            result.ChatFolder = chatFolder;
            _videosDatabase.Chats.Add(result);
            _logger.LogInformation("{folder} added.", chatFolder.Name);
        }

        _logger.LogInformation("Saving...");
        await _videosDatabase.SaveChangesAsync();
        _logger.LogInformation("Saved.");
    }

    public async Task<List<(int albumId, string title, List<(string? mediaItemId, long chatId, int messageId)> items)>> GetAlbums()
    {
        var albums = await _videosDatabase.Albums
            .Where(x => x.UploadedAlbum == null)
            .Include(x => x.Constraints)
            .ThenInclude(x => x.Topic)
            .ToListAsync();

        var messages = (await _videosDatabase.Messages
                .Where(x => x.SelectedType.HasValue && x.TopicId.HasValue)
                .Select(m => new
                {
                    m.TopicId,
                    m.ChatId,
                    m.Width,
                    m.Height,
                    m.Date,
                    m.Id,
                    m.SelectedType,
                })
                .ToListAsync())
            .ToLookup(x => x.TopicId!.Value);

        var uploadedItems = await _videosDatabase.UploadedItems
            .Where(i => _videosDatabase.InventoryItems.Any(j => j.Id == i.MediaItemId && (j.IsPhoto || j.MediaMetadataVideoStatus == "READY")))
            .ToDictionaryAsync(x => new { x.ChatId, x.MessageId, }, x => x.MediaItemId);

        return albums
            .Select(a => (a.Id, a.Title,
                a.Constraints
                    .SelectMany(c => messages[c.TopicId!.Value].Where(m =>
                    {
                        if (c.MessageSelectedType.HasValue && c.MessageSelectedType != m.SelectedType) return false;
                        if (c.IsSquare && m.Width != m.Height) return false;
                        if (!c.Include) throw new("The exclusion is not supported yet.");
                        if (c.After.HasValue && m.Date < c.After) return false;
                        if (c.Before.HasValue && m.Date > c.Before) return false;
                        return true;
                    }))
                    .Select(x => new { x.ChatId, MessageId = x.Id })
                    .Distinct()
                    .Select(x => (uploadedItems.GetValueOrDefault(x), x.ChatId, x.MessageId))
                    .ToList()))
            .ToList();
    }
}