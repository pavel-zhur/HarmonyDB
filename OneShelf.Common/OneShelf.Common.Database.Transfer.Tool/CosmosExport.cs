using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OneShelf.Collectives.Database;
using OneShelf.Collectives.Database.Models;
using OneShelf.Common.Database;
using OneShelf.Illustrations.Database;
using OneShelf.Illustrations.Database.Models;

namespace OneShelf.Common.Database.Transfer.Tool;

/// <summary>
/// Loads the stores that were kept in Cosmos DB from an export of its containers: a file per container, a document per line,
/// as the Cosmos DB client wrote them (<see cref="DocumentJson"/>). The documents go in through the stores, as the services add them.
/// Their times were written in UTC, and are read in the local time of the tool, as the services read them: run it in UTC.
/// </summary>
internal static class CosmosExport
{
    /// <summary>
    /// Settings: Source, the export of the collectives' container.
    /// </summary>
    public static async Task<bool> LoadCollectives(CollectivesDatabase target, IConfigurationSection settings)
    {
        var documents = 0;
        foreach (var line in File.ReadLines(GetSetting(settings, "Source")))
        {
            await target.UpsertCollective(DocumentJson.Deserialize<Collective>(line));
            documents++;
        }

        return Compare("collectives", documents, await target.Collectives.CountAsync());
    }

    /// <summary>
    /// Settings: PromptsSource and IllustrationsSource, the exports of the prompts' and the illustrations' containers.
    /// Every illustration gets its copies, made from its image; the public addresses the documents had are not taken.
    /// </summary>
    public static async Task<bool> LoadIllustrations(IllustrationsDatabase target, IConfigurationSection settings)
    {
        var prompts = 0;
        foreach (var line in File.ReadLines(GetSetting(settings, "PromptsSource")))
        {
            await target.AddPrompts(DocumentJson.Deserialize<IllustrationsPrompts>(line));
            prompts++;
        }

        var illustrations = 0;
        foreach (var line in File.ReadLines(GetSetting(settings, "IllustrationsSource")))
        {
            await target.AddIllustration(DocumentJson.Deserialize<Illustration>(line));
            if (++illustrations % 200 == 0) Log($"  {illustrations} illustrations");
        }

        return Compare("prompts", prompts, await target.Prompts.CountAsync())
               & Compare("illustrations", illustrations, await target.Illustrations.CountAsync())
               & Compare("copies", illustrations * IllustrationsDatabase.CopySizes.Count, await target.IllustrationCopies.CountAsync());
    }

    private static bool Compare(string what, int expected, int stored)
    {
        var equal = expected == stored;
        Log($"  {what}: {expected} in the export, {stored} stored{(equal ? "" : ", NOT EQUAL")}.");
        return equal;
    }

    private static string GetSetting(IConfigurationSection settings, string key)
        => settings[key] is { Length: > 0 } value ? value : throw new($"The setting {settings.Path}:{key} is missing.");

    private static void Log(string message) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {message}");
}
