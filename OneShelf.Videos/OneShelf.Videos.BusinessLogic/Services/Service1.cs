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

    public async Task<List<(int mediaId, string path, DateTime publishedOn)>> GetExport1()
    {
        var messages = await _videosDatabase.Mediae
            .Where(x => x.StaticMessage != null)
            .Where(x => x.StaticMessage!.SelectedType == StaticMessageSelectedType.Photo)
            .Where(x => x.StaticMessage!.Media != null)
            .Where(x => x.UploadedItem == null)
            .Include(x => x.StaticMessage)
            .ThenInclude(x => x.StaticChat)
            .ThenInclude(x => x.StaticChatFolder)
            .ToListAsync();

        return messages
            .Select(x => (x.Id, Path.Combine(x.StaticMessage!.StaticChat.StaticChatFolder.Root, x.StaticMessage.Photo!), x.StaticMessage.Date))
            .ToList();
    }

    public async Task<List<(int mediaId, string path, DateTime publishedOn)>> GetExport2()
    {
        var messages = await _videosDatabase.Mediae
            .Where(x => x.StaticMessage != null)
            .Where(x => x.StaticMessage!.SelectedType == StaticMessageSelectedType.Video)
            .Where(x => x.StaticMessage!.Media != null)
            .Where(x => x.UploadedItem == null)
            .Include(x => x.StaticMessage)
            .ThenInclude(x => x.StaticChat)
            .ThenInclude(x => x.StaticChatFolder)
            .ToListAsync();

        return messages
            .Select(x => (x.Id, Path.Combine(x.StaticMessage!.StaticChat.StaticChatFolder.Root, x.StaticMessage.File!), x.StaticMessage.Date))
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

    public async Task<List<(int albumId, string title, List<(string mediaItemId, int mediaId)> items)>> GetAlbums()
    {
        var albums = await _videosDatabase.Albums
            .Where(x => x.UploadedAlbum == null)
            .Include(x => x.Constraints)
            .ThenInclude(x => x.StaticTopic)
            .ToListAsync();

        var messages = (await _videosDatabase.Mediae
                .Where(x => x.StaticMessage != null)
                .Where(x => x.StaticMessage!.SelectedType.HasValue && x.StaticMessage.StaticTopicId.HasValue)
                .Select(m => new
                {
                    TopicId = m.StaticMessage!.StaticTopicId,
                    m.StaticMessage.Width,
                    m.StaticMessage.Height,
                    m.StaticMessage.Date,
                    m.StaticMessage.SelectedType,
                    m.Id,
                })
                .ToListAsync())
            .ToLookup(x => x.TopicId!.Value);

        var uploadedItems = await _videosDatabase.UploadedItems
            .Where(i => _videosDatabase.InventoryItems.Any(j => j.Id == i.MediaItemId && (j.IsPhoto || j.MediaMetadataVideoStatus == "READY")))
            .ToDictionaryAsync(x => x.MediaId, x => x.MediaItemId);

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
                    .Select(x => x.Id)
                    .Distinct()
                    .Select(x => (mediaItemId: uploadedItems.GetValueOrDefault(x), x))
                    .Where(x => x.mediaItemId != null)
                    .Select(x => (x.mediaItemId!, x.x))
                    .ToList()))
            .ToList();
    }
}