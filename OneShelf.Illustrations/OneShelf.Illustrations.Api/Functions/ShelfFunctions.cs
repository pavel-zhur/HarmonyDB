using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OneShelf.Common;
using OneShelf.Common.Api;
using OneShelf.Illustrations.Api.Constants;
using OneShelf.Illustrations.Api.Model;
using OneShelf.Illustrations.Api.Services;
using OneShelf.Illustrations.Database;

namespace OneShelf.Illustrations.Api.Functions;

/// <summary>
/// The illustrations of the new shelf, for internal clients with the scope <see cref="IllustrationsModule.InternalScope"/>.
/// They belong to a key (the chords' id there) and are drawn by a preset: the system message
/// <see cref="SystemMessage"/>, the one the old shelf recommends, plain or with one of its alterations (a mood). A preset
/// is drawn for a key once, as the old shelf draws it when one looks: the attempt of the prompts that suits best, all its
/// images, each kept as soon as it is drawn; ordered again, only what is missing of it is drawn; while it is drawn, an
/// order of it waits for that drawing (<see cref="ShelfDrawings"/>). A key with an alias (<see cref="ShelfAlias"/>: an item
/// the old shelf illustrated under its address) is read and drawn under that address, so the old illustrations show and
/// new ones join them. A request that cannot be read is answered 400.
/// </summary>
public class ShelfFunctions(IllustrationsDatabase illustrationsDatabase, IllustrationsGenerator illustrationsGenerator, ShelfDrawings drawings)
{
    public const int SystemMessage = 5;

    /// <summary>
    /// The preset with no alteration.
    /// </summary>
    public const string PlainPreset = "plain";

    /// <summary>
    /// The plain preset's mood.
    /// </summary>
    public const string PlainMood = "plain";

    public static IReadOnlyList<ShelfPreset> Presets { get; } = SystemMessages.Alterations
        .Select(x => new ShelfPreset { Key = x.Key, Mood = x.Value.mood })
        .Prepend(new() { Key = PlainPreset, Mood = PlainMood })
        .ToList();

    /// <summary>
    /// The preset's key images were drawn by, by their prompts; null for prompts made otherwise.
    /// </summary>
    private static string? PresetOf(OneShelf.Illustrations.Database.Models.IllustrationsPrompts? prompts)
        => prompts is { SpecialSystemMessage: SystemMessage } ? prompts.AlterationKey ?? PlainPreset : null;

    /// <summary>
    /// A preset's mood; null for no preset, or one no longer offered.
    /// </summary>
    private static string? MoodOf(string? preset) => Presets.SingleOrDefault(x => x.Key == preset)?.Mood;

    [Function(IllustrationsApiUrls.ShelfPresets, AuthorizationLevel.Internal, "post", InternalScope = IllustrationsModule.InternalScope)]
    public IActionResult GetPresets(HttpRequest req) => new OkObjectResult(new ShelfPresetsResponse
    {
        Presets = Presets.ToList(),
        Sizes = IllustrationsDatabase.CopySizes.ToList(),
    });

    [Function(IllustrationsApiUrls.ShelfRead, AuthorizationLevel.Internal, "post", InternalScope = IllustrationsModule.InternalScope)]
    public async Task<IActionResult> Read(HttpRequest req, [FromBody] ShelfReadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Key)) return new BadRequestResult();

        var url = await illustrationsDatabase.GetShelfUrl(request.Key);
        var prompts = (await illustrationsDatabase.GetPrompts(url)).ToDictionary(x => x.Id);
        var images = await illustrationsDatabase.GetIllustrationHeaders(url);

        return new OkObjectResult(new ShelfReadResponse
        {
            Images = images
                .OrderBy(x => x.CreatedOn)
                .ThenBy(x => x.AttemptIndex)
                .ThenBy(x => x.PhotoIndex)
                .Select(x => new ShelfImage
                {
                    Id = x.Id,
                    CreatedOn = x.CreatedOn,
                    Preset = PresetOf(prompts.GetValueOrDefault(x.PromptsId)),
                    Mood = MoodOf(PresetOf(prompts.GetValueOrDefault(x.PromptsId))),
                })
                .ToList(),
        });
    }

    /// <summary>
    /// The covers of many keys at once: for each that has illustrations, the first of them, in one query.
    /// </summary>
    [Function(IllustrationsApiUrls.ShelfCovers, AuthorizationLevel.Internal, "post", InternalScope = IllustrationsModule.InternalScope)]
    public async Task<IActionResult> Covers(HttpRequest req, [FromBody] ShelfCoversRequest request)
    {
        if (request?.Keys == null! || request.Keys.Count > ShelfCoversRequest.MaxKeys || request.Keys.Any(string.IsNullOrWhiteSpace))
            return new BadRequestResult();

        var covers = await illustrationsDatabase.GetShelfCovers(request.Keys);
        return new OkObjectResult(new ShelfCoversResponse
        {
            Covers = covers.Select(x => new ShelfCover { Key = x.Key, Id = x.Id }).ToList(),
        });
    }

    [Function(IllustrationsApiUrls.ShelfGenerate, AuthorizationLevel.Internal, "post", InternalScope = IllustrationsModule.InternalScope)]
    public async Task<IActionResult> Generate(HttpRequest req, [FromBody] ShelfGenerateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Key) || request.Lyrics == null! || Presets.All(x => x.Key != request.Preset))
            return new BadRequestResult();

        // one drawing of a preset for a key at a time: an order while one is drawn waits for it and gets its answer
        return new OkObjectResult(await drawings.RunOnceAsync((request.Key, request.Preset), () => DrawAsync(request)));
    }

    /// <summary>
    /// Draws what is missing of the preset's batch for the key: the prompts, when there are none yet; then every image of
    /// the chosen attempt that has none (all of them the first time; the rest after a batch cut short or partly failed).
    /// </summary>
    private async Task<ShelfGenerateResponse> DrawAsync(ShelfGenerateRequest request)
    {
        var url = await illustrationsDatabase.GetShelfUrl(request.Key);
        var alterationKey = request.Preset == PlainPreset ? null : request.Preset;
        var prompts = await illustrationsGenerator.GetOrCreatePrompts(
            url, () => Task.FromResult<string?>(request.Lyrics), SystemMessage, null, alterationKey, null, request.AdditionalBillingInfo);

        if (prompts == null)
            return Answer(ShelfGenerateOutcome.NoLyrics, 0);

        var drawn = (await illustrationsDatabase.GetIllustrationHeaders(url)).Where(x => x.PromptsId == prompts.Id).ToList();

        // the attempt drawn before; else the one the old shelf draws: up to 7 images, the closer to 5 the better; else the
        // fewer the better; then the longer descriptions
        var attempt = drawn.Count > 0
            ? drawn[0].AttemptIndex
            : prompts.Prompts
                .WithIndices()
                .Select(x => (x.i, x.x.Count, length: x.x.Sum(p => p.Length)))
                .Where(x => x.Count > 0)
                .OrderBy(x => x.Count > 7 ? 2 : 1)
                .ThenBy(x => x.Count > 7 ? x.Count : Math.Abs(5 - x.Count))
                .ThenBy(x => x.Count)
                .ThenByDescending(x => x.length)
                .Select(x => (int?)x.i)
                .FirstOrDefault();

        if (attempt is not { } i)
            return Answer(ShelfGenerateOutcome.NoPrompts, 0);

        var missing = prompts.Prompts[i]
            .Select((_, j) => new GenerationIndex(i, j))
            .Where(x => drawn.All(d => d.AttemptIndex != x.AttemptIndex || d.PhotoIndex != x.PhotoIndex))
            .ToList();
        if (missing.Count == 0)
            return Answer(ShelfGenerateOutcome.AlreadyMade, 0);

        var made = await illustrationsGenerator.GenerateImages(url, prompts, missing, null, request.AdditionalBillingInfo);
        return Answer(ShelfGenerateOutcome.Made, made);
    }

    /// <summary>
    /// Puts the new shelf's aliases in bulk (<see cref="IllustrationsDatabase.PutShelfAliases"/>): idempotent.
    /// </summary>
    [Function(IllustrationsApiUrls.ShelfAliases, AuthorizationLevel.Internal, "post", InternalScope = IllustrationsModule.AliasesScope)]
    public async Task<IActionResult> PutAliases(HttpRequest req, [FromBody] ShelfAliasesRequest request)
    {
        if (request?.Aliases == null! || request.Aliases.Any(x => string.IsNullOrWhiteSpace(x?.Key) || string.IsNullOrWhiteSpace(x.Url)))
            return new BadRequestResult();

        var result = await illustrationsDatabase.PutShelfAliases(request.Aliases.Select(x => (x.Key, x.Url)).ToList());
        return new OkObjectResult(new ShelfAliasesResponse
        {
            Added = result.Added,
            Unchanged = result.Unchanged,
            NotIllustrated = result.NotIllustrated,
            OwnAlready = result.OwnAlready,
            IllustratedWithoutAlias = result.IllustratedWithoutAlias,
        });
    }

    private static ShelfGenerateResponse Answer(ShelfGenerateOutcome outcome, int made) => new() { Outcome = outcome, Made = made };
}

/// <summary>
/// The drawings going on in this service, by key and preset: what is being drawn is not ordered again (the service runs
/// as one copy).
/// </summary>
public sealed class ShelfDrawings
{
    private readonly Dictionary<(string Key, string Preset), Task<ShelfGenerateResponse>> _running = new();

    public Task<ShelfGenerateResponse> RunOnceAsync((string Key, string Preset) what, Func<Task<ShelfGenerateResponse>> draw)
    {
        lock (_running)
        {
            if (_running.TryGetValue(what, out var running))
                return running;

            var task = RunAsync(what, draw);
            _running[what] = task;
            return task;
        }
    }

    private async Task<ShelfGenerateResponse> RunAsync((string Key, string Preset) what, Func<Task<ShelfGenerateResponse>> draw)
    {
        try
        {
            // never inline: the entry is in place before the drawing can end
            await Task.Yield();
            return await draw();
        }
        finally
        {
            lock (_running)
                _running.Remove(what);
        }
    }
}