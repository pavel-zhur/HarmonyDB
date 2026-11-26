using System.Xml.Linq;

namespace OneShelf.Api.Tests;

/// <summary>
/// The modules of the common application are kept apart: a project of one module references another module only
/// through that module's client and API model (<c>*.Api.Client</c>, <c>*.Api.Model</c>) — never its application, its
/// database or its logic; and nobody but the old shelf's own reaches into the old shelf's songs database. What was
/// there before this rule is listed below by name, project by project, so that nothing new slips in beside it; and a
/// listed reach that is gone is to be taken off the list.
/// </summary>
public sealed class ModuleSeparationTests
{
    /// <summary>
    /// The modules, by their folders (each a separate participant, whether it runs in the common application or alone).
    /// </summary>
    private static readonly string[] Modules =
    [
        "HarmonyDB.Analyses",
        "HarmonyDB.Index",
        "OneShelf.Authorization",
        "OneShelf.Billing",
        "OneShelf.Collectives",
        "OneShelf.Frontend",
        "OneShelf.Illustrations",
        "OneShelf.OneDog",
        "OneShelf.OneDragon",
        "OneShelf.Pdfs",
        "OneShelf.Sources",
        "OneShelf.Telegram",
    ];

    /// <summary>
    /// The old shelf's songs database: not a module's, and still reached into by the old modules.
    /// </summary>
    private const string SongsDatabase = "OneShelf.Common.Database.Songs";

    /// <summary>
    /// The reaches kept from before, knowingly: (project, the project it references).
    /// </summary>
    private static readonly (string From, string To)[] Inherited =
    [
        // the old modules' reaches into the old shelf's songs database
        ("OneShelf.Authorization.Api", SongsDatabase),
        ("OneShelf.Billing.Api", SongsDatabase),
        ("OneShelf.Collectives.Api", SongsDatabase),
        ("OneShelf.Frontend.Api", SongsDatabase),
        ("OneShelf.Pdfs.Generation.Inspiration", SongsDatabase),
        ("OneShelf.Telegram.Processor", SongsDatabase),

        // the own chords' source's two foreign models
        ("OneShelf.Sources.Self.Api", "OneShelf.Frontend.SpecificModel"),
        ("OneShelf.Sources.Self.Api", "OneShelf.Pdfs.SpecificModel"),

        // the old shelf's PDFs, made in its own process
        ("OneShelf.Frontend.Api", "OneShelf.Pdfs.Generation.Inspiration"),
        ("OneShelf.Frontend.Api", "OneShelf.Pdfs.Generation.Volumes"),
        ("OneShelf.Frontend.Api", "OneShelf.Pdfs.SpecificModel"),
        ("OneShelf.Telegram.Processor", "OneShelf.Pdfs.Generation.Inspiration"),

        // the old web app's tool that generates its references
        ("DotnetReference", "HarmonyDB.Index.Analysis"),

        // the bots built on the old shelf's bot
        ("OneShelf.OneDog.Database", "OneShelf.Telegram.Ai.Model"),
        ("OneShelf.OneDog.Processor", "OneShelf.Telegram"),
        ("OneShelf.OneDog.Processor", "OneShelf.Telegram.Ai"),
        ("OneShelf.OneDog.Processor", "OneShelf.Telegram.Ai.Model"),
        ("OneShelf.OneDragon.Database", "OneShelf.Telegram.Ai.Model"),
        ("OneShelf.OneDragon.Processor", "OneShelf.Telegram"),
        ("OneShelf.OneDragon.Processor", "OneShelf.Telegram.Ai"),
    ];

    [Fact]
    public void AModuleReachesAnotherOnlyThroughItsClientAndModel()
    {
        var root = Root();
        var found = new List<(string From, string To)>();
        foreach (var module in Modules)
        {
            foreach (var project in Directory.EnumerateFiles(Path.Combine(root, module), "*.csproj", SearchOption.AllDirectories)
                         .Where(x => !x.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or "node_modules"))
                         // a module's tests may host other modules beside it, as the common application does
                         .Where(x => !Path.GetFileNameWithoutExtension(x).EndsWith(".Tests", StringComparison.Ordinal)))
            {
                var directory = Path.GetDirectoryName(project)!;
                foreach (var include in XDocument.Load(project).Descendants("ProjectReference").Select(x => (string?)x.Attribute("Include")).OfType<string>())
                {
                    var target = Path.GetFullPath(Path.Combine(directory, include.Replace('\\', Path.DirectorySeparatorChar)));
                    var targetName = Path.GetFileNameWithoutExtension(target);
                    var targetModule = Path.GetRelativePath(root, target).Split(Path.DirectorySeparatorChar)[0];

                    var intoAnother = Modules.Contains(targetModule) && targetModule != module
                                      && !targetName.EndsWith(".Api.Client", StringComparison.Ordinal)
                                      && !targetName.EndsWith(".Api.Model", StringComparison.Ordinal);
                    if (intoAnother || targetName == SongsDatabase)
                        found.Add((Path.GetFileNameWithoutExtension(project), targetName));
                }
            }
        }

        Assert.Empty(found.Except(Inherited));
        Assert.Empty(Inherited.Except(found));
    }

    /// <summary>
    /// The root of the source tree: the folder of the solution, above the tests' binaries.
    /// </summary>
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HarmonyDB and OneShelf.sln")))
                return directory.FullName;
        }

        throw new InvalidOperationException("The source tree is not above the tests.");
    }
}
