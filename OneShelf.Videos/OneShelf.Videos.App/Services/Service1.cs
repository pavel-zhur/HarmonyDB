using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CsvHelper;
using Microsoft.Extensions.Options;
using OneShelf.Videos.App.ChatModels;
using OneShelf.Videos.App.Database.Models;
using OneShelf.Videos.App.Models;

namespace OneShelf.Videos.App.Services;

public class Service1
{
    private readonly IOptions<VideosOptions> _options;
    //private readonly string _path = "Y:\\";
    private List<(string pathPrefix, Chat chat)>? _chats;

    public Service1(IOptions<VideosOptions> options)
    {
        _options = options;
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

    public void Initialize()
    {
        var actualFiles = ReadActualFiles();

        _chats = actualFiles
            .Select(x =>
            {
                var path = x.FullName;
                var index = path.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
                if (index == -1) return null;
                index = path.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], index + 1);
                if (index == -1) return null;
                return path.Substring(0, index + 1);
            })
            .Where(x => x != null)
            .Distinct()
            .OrderBy(x => x)
            .Zip(Directory.GetFiles(_options.Value.BasePath, "result (?).json")
                .Select(f =>
                {
                    try
                    {
                        return JsonSerializer.Deserialize<Chat>(File.ReadAllText(f), new JsonSerializerOptions
                        {
                            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                        });
                    }
                    catch
                    {
                        Console.WriteLine(f);
                        throw;
                    }
                }))
            .Select(x => (pathPrefix: x.First!, chat: x.Second!))
            .ToList();
    }

    private async Task ExportMessages()
    {
        await using var fileStream = File.Create(Path.Combine(_options.Value.BasePath, "messages.csv"));
        await using var textWriter = new StreamWriter(fileStream, Encoding.UTF8);
        await using var csvWriter = new CsvWriter(textWriter, CultureInfo.InvariantCulture);
        await csvWriter.WriteRecordsAsync(_chats!.SelectMany(c => c.chat.Messages.Select(m => new
        {
            c.chat.Id,
            c.chat.Name,
            c.chat.Type,
            c.pathPrefix,
            Text = (string)JsonSerializer.Serialize(m.Text),
            m,
        })));
    }

    private List<ActualFile> ReadActualFiles()
    {
        return JsonSerializer.Deserialize<List<ActualFile>>(File.ReadAllText(GetAllJsonPath()))!;
    }

    private async Task SaveActualFiles()
    {
        var actualFiles = new DirectoryInfo("y:").GetFiles("*.*", SearchOption.AllDirectories).Where(x => !x.FullName.StartsWith(Path.Combine(_options.Value.BasePath, "_app"))).Select(x => new ActualFile(x.FullName, x.Length)).ToList();
        await File.WriteAllTextAsync(GetAllJsonPath(), JsonSerializer.Serialize(actualFiles, new JsonSerializerOptions { WriteIndented = true, }));
    }

    private string GetAllJsonPath()
    {
        return Path.Combine(_options.Value.BasePath, "all.json");
    }

}