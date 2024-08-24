using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Videos.App.Database;
using OneShelf.Videos.App.Database.Models;
using OneShelf.Videos.App.Database.Models.Json;
using OneShelf.Videos.App.Models;

namespace OneShelf.Videos.App.Services;

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
            .Where(x => x.Photo != null)
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
            .Where(x => x.MimeType!.ToLower().StartsWith("video/") == true)
            .Where(x => x.MediaType == "video_file" || x.MediaType == null)
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

}