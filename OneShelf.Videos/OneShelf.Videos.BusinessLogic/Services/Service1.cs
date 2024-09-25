using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Videos.BusinessLogic.Models;
using OneShelf.Videos.Database;
using OneShelf.Videos.Database.Models.Static;

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
        var messages = await _videosDatabase.StaticMessages
            .Where(x => x.SelectedType == StaticMessageSelectedType.Photo)
            .Include(x => x.StaticChat)
            .ThenInclude(x => x.StaticChatFolder)
            .Where(x => !_videosDatabase.UploadedItems.Any(y => y.StaticChatId == x.StaticChatId && y.StaticMessageId == x.Id))
            .ToListAsync();

        return messages
            .Select(x => (x.StaticChat.Id, x.Id, Path.Combine(x.StaticChat.StaticChatFolder.Root, x.Photo!), x.Date))
            .ToList();
    }

    public async Task<List<(long chatId, int messageId, string path, DateTime publishedOn)>> GetExport2()
    {
        var messages = await _videosDatabase.StaticMessages
            .Where(x => x.SelectedType == StaticMessageSelectedType.Video)
            .Include(x => x.StaticChat)
            .ThenInclude(x => x.StaticChatFolder)
            .Where(x => !_videosDatabase.UploadedItems.Any(y => y.StaticChatId == x.StaticChatId && y.StaticMessageId == x.Id))
            .ToListAsync();

        return messages
            .Select(x => (x.StaticChat.Id, x.Id, Path.Combine(x.StaticChat.StaticChatFolder.Root, x.File!), x.Date))
            .ToList();
    }

    public async Task SaveChatFolders()
    {
        var existing = await _videosDatabase.StaticChatFolders.ToListAsync();
        foreach (var directory in Directory.GetDirectories(_options.Value.BasePath, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(directory);
            if (existing.Any(x => x.Name == name)) continue;

            if (!File.Exists(Path.Combine(directory, "result.json"))) continue;

            _videosDatabase.StaticChatFolders.Add(new()
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
        foreach (var (chatFolder, i) in (await _videosDatabase.StaticChatFolders.Where(f => f.StaticChat == null).ToListAsync()).WithIndices())
        {
            var result = JsonSerializer.Deserialize<StaticChat>(await File.ReadAllTextAsync(chatFolder.ResultJsonFullPath), new JsonSerializerOptions
            {
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            })!;

            result.StaticChatFolder = chatFolder;
            _videosDatabase.StaticChats.Add(result);
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
            .ThenInclude(x => x.StaticTopic)
            .ToListAsync();

        var messages = (await _videosDatabase.StaticMessages
                .Where(x => x.SelectedType.HasValue && x.StaticTopicId.HasValue)
                .Select(m => new
                {
                    TopicId = m.StaticTopicId,
                    ChatId = m.StaticChatId,
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
            .ToDictionaryAsync(x => new { ChatId = x.StaticChatId, MessageId = x.StaticMessageId, }, x => x.MediaItemId);

        return albums
            .Select(a => (a.Id, a.Title,
                a.Constraints
                    .SelectMany(c => messages[c.StaticTopicId!.Value].Where(m =>
                    {
                        if (c.StaticMessageSelectedType.HasValue && c.StaticMessageSelectedType != m.SelectedType) return false;
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