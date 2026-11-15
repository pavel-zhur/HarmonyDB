using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace HarmonyDB.Theory.Chords.Tests;

public static class Resources
{
    private const string AllChordsResourceName = "HarmonyDB.Theory.Chords.Tests.Resources.AllChords.json.gz";

    static Resources()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(AllChordsResourceName)!;
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        AllChords = JsonSerializer.Deserialize<Dictionary<string, int>>(gzip)!;
    }

    public static IReadOnlyDictionary<string, int> AllChords { get; }
}