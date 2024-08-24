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
    private List<(string pathPrefix, Chat chat)>? _chats;
    private readonly VideosDatabase _videosDatabase;
    private readonly ILogger<Service1> _logger;

    public Service1(IOptions<VideosOptions> options, VideosDatabase videosDatabase, ILogger<Service1> logger)
    {
        _options = options;
        _videosDatabase = videosDatabase;
        _logger = logger;
    }

    public List<(long chatId, int messageId, string path, DateTime publishedOn)> GetExport1()
    {
        return _chats!
            .SelectMany(c => c.chat.Messages
                .Where(x => x.Photo != null)
                .Select(x => (c.chat.Id, x.Id, Path.Combine(c.pathPrefix, x.Photo!), x.Date)))
            .OrderBy(_ => Random.Shared.NextDouble())
            .ToList();
    }

    public List<(long chatId, int messageId, string path, DateTime publishedOn)> GetExport2()
    {
        return _chats!
            .SelectMany(c => c.chat.Messages
                .Where(x => x.MimeType?.StartsWith("video/", StringComparison.InvariantCultureIgnoreCase) == true)
                .Where(x => x.MediaType == "video_file") // todo: more video files without this filter may exist. the filter is too narrow.
                .Select(x => (c.chat.Id, x.Id, Path.Combine(c.pathPrefix, x.File!), x.Date)))
            .OrderBy(_ => Random.Shared.NextDouble())
            .ToList();
    }

    public async Task SaveChatFolders()
    {
        foreach (var directory in Directory.GetDirectories(_options.Value.BasePath, "*", SearchOption.TopDirectoryOnly))
        {
            if (File.Exists(Path.Combine(directory, "result.json")))
            {
                _videosDatabase.ChatFolders.Add(new()
                {
                    Name = Path.GetFileName(directory),
                    Root = directory,
                });
            }
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