using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CsvHelper;
using OneShelf.Videos.App.ChatModels;
using OneShelf.Videos.App.Models;

namespace OneShelf.Videos.App;

public class Service1
{
    public async Task Task1()
    {
        var path = "C:\\temp\\telegram google photos";

        //await ListActualFiles(path);
        var actualFiles = ReadActualFiles(path);
        var files = Directory.GetFiles(path, "result*.json");

        var chats = actualFiles
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
            .Zip(files.Select(f =>
            {
                try
                {
                    return JsonSerializer.Deserialize<Chat>(File.ReadAllText(f), new JsonSerializerOptions
                    {
                        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine(f);
                    throw;
                }
            }))
            .Select(x => (pathPrefix: x.First, chat: x.Second))
            .ToList();

        Console.WriteLine(string.Join(", ", chats.Select(x => x.pathPrefix)));

        await ExportMessages(path, chats);
    }

    private async Task ExportMessages(string path, List<(string? pathPrefix, Chat? chat)> chats)
    {
        using var fileStream = File.Create(Path.Combine(path, "messages.csv"));
        using var textWriter = new StreamWriter(fileStream, Encoding.UTF8);
        using var csvWriter = new CsvWriter(textWriter, CultureInfo.InvariantCulture);
        await csvWriter.WriteRecordsAsync(chats.SelectMany(c => c.chat.Messages.Select(m => new
        {
            c.pathPrefix,
            m
        })));
    }

    private List<ActualFile> ReadActualFiles(string path)
    {
        return JsonSerializer.Deserialize<List<ActualFile>>(File.ReadAllText(GetAllJsonPath(path)))!;
    }

    private async Task ListActualFiles(string path)
    {
        var actualFiles = new DirectoryInfo("y:").GetFiles("*.*", SearchOption.AllDirectories).Select(x => new ActualFile(x.FullName, x.Length)).ToList();
        await File.WriteAllTextAsync(GetAllJsonPath(path), JsonSerializer.Serialize(actualFiles, new JsonSerializerOptions { WriteIndented = true, }));
    }

    private string GetAllJsonPath(string path)
    {
        return Path.Combine(path, "all.json");
    }

}