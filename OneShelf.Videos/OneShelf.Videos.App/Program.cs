using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CsvHelper;
using OneShelf.Videos.App.ChatModels;
using OneShelf.Videos.App.Models;

namespace OneShelf.Videos.App;

public class Program
{
    static async Task Main()
    {
        var path = "C:\\temp\\telegram google photos";
        var files = Directory.GetFiles(path, "result*.json");
        var chats = files.Select(f =>
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
        }).ToList();

        //await ListActualFiles(path);
        var actualFiles = ReadActualFiles(path);

        //await ExportMessages(path, chats);
    }

    private static async Task ExportMessages(string path, List<Chat?> chats)
    {
        using var fileStream = File.Create(Path.Combine(path, "messages.csv"));
        using var textWriter = new StreamWriter(fileStream);
        using var csvWriter = new CsvWriter(textWriter, CultureInfo.InvariantCulture);
        await csvWriter.WriteRecordsAsync(chats.SelectMany(c => c.Messages));
    }

    private static List<ActualFile> ReadActualFiles(string path)
    {
        return JsonSerializer.Deserialize<List<ActualFile>>(File.ReadAllText(GetAllJsonPath(path)))!;
    }

    private static async Task ListActualFiles(string path)
    {
        var actualFiles = new DirectoryInfo("y:").GetFiles("*.*", SearchOption.AllDirectories).Select(x => new ActualFile(x.FullName, x.Length)).ToList();
        await File.WriteAllTextAsync(GetAllJsonPath(path), JsonSerializer.Serialize(actualFiles, new JsonSerializerOptions { WriteIndented = true, }));
    }

    private static string GetAllJsonPath(string path)
    {
        return Path.Combine(path, "all.json");
    }
}