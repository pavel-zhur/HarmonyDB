using System.ComponentModel;
using System.Security.Claims;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Areas.Mcp.Services;
using HarmonyDB.Shelf.Api.Areas.Media.Services;
using HarmonyDB.Shelf.Api.Areas.OAuth.Services;
using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Source.Api.Model.V2.Api;
using AnalysisToolsService = HarmonyDB.Shelf.Api.Areas.AnalysisTools.Services.AnalysisTools;

namespace HarmonyDB.Shelf.Api.Areas.Mcp.Tools;

/// <summary>
/// The index: chords from the sources — searched, opened, found by a link — and the analysis tools.
/// A handler's parameters: the services and the caller first, then the tool's arguments — the optional ones with
/// their defaults (an argument without a default is required).
/// </summary>
public static class IndexTools
{
    /// <summary>
    /// How the text form of a chord sheet reads; also the markup of own chords.
    /// </summary>
    public const string SheetFormat =
        "Text format: a line starting with '>' holds only chords (played over the next line); '<Am>' inside a line is a " +
        "chord before the word it falls on; '[Chorus]' is a heading.";

    public static IEnumerable<McpTool> All() =>
    [
        new("search_chords",
            "Search the chords of songs in all sources by title and/or artist. Returns items with chordsId (used by every " +
            "other tool), title, artists, source, stated key. Several items per song are usual. failedSources: sources " +
            "that did not answer.",
            McpToolEffect.Read,
            async (
                ClaimsPrincipal user, IndexClient index, SourceCallers callers,
                [Description("Title and/or artist words.")] string query,
                int? offset = null,
                [Description("1-25, default 10.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                var text = string.Join(' ', (query ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                if (text.Length is < 2 or > 200)
                    throw ShelfException.BadRequest();

                var found = await index.Sources.Search(callers.For(user), text, cancellationToken);
                return new
                {
                    chords = Slim.Paged(found.Headers, offset, limit, x => Slim.Chords(ChordsReader.ToHeader(x))),
                    failedSources = found.Failures.Count > 0 ? found.Failures.Select(x => x.Source).ToList() : null,
                };
            },
            BuiltInPermissions.McpIndex),

        new("get_chords",
            "Open chords by chordsId. Default kind 'sheet': the song as text with chords, optionally transposed. " + SheetFormat +
            " Other kinds an item may have (see its kinds): timeline, lyrics, notes, score, chart, recording — answered " +
            "with a short summary and a link (url, valid minutes) to the full JSON. state notOpen: not open to this person. " +
            "A sheet its source does not vouch for comes without its text: unstableMessage (the source's words) and the " +
            "address of its page instead.",
            McpToolEffect.Read,
            async (
                ClaimsPrincipal user, ChordsReader reader, McpFiles files,
                string chordsId,
                [Description("sheet (default), timeline, lyrics, notes, score, chart, recording.")] ChordsKind? kind = null,
                [Description("Sheet only: semitones -11..11.")] int? transpose = null,
                CancellationToken cancellationToken = default) =>
            {
                if (string.IsNullOrWhiteSpace(chordsId) || transpose is < -11 or > 11)
                    throw ShelfException.BadRequest();

                var asked = kind ?? ChordsKind.Sheet;
                var answer = await reader.GetContentAsync(user, chordsId, [ChordsReader.ToContentKind(asked)], null, cancellationToken);
                if (answer.State != ChordsState.Open)
                    return (object)new { chordsId, state = answer.State };

                if (asked == ChordsKind.Sheet)
                {
                    var sheet = answer.Content?.Sheet;
                    // as a person is shown it: the source's words and its page in the place of a sheet it does not vouch for
                    if (sheet?.UnstableMessage is { } unstable)
                        return new { chords = Slim.Chords(answer.Header!), unstableMessage = unstable, address = answer.Header!.Address };

                    var settings = new RepresentationSettings(transpose ?? 0);
                    return new
                    {
                        chords = Slim.Chords(answer.Header!),
                        transpose = transpose ?? 0,
                        keys = sheet?.Root.Keys?.Select(x => x.Shown(settings).Name).ToList(),
                        text = sheet?.Root.AsOneShelf(settings),
                        authorComment = Slim.Short(sheet?.AuthorComment, 500),
                    };
                }

                if (answer.Content == null || !answer.Header!.Kinds.Contains(asked))
                    return new { chords = Slim.Chords(answer.Header!), kind = asked, has = false };

                var (url, expiresAt) = files.Content(user, chordsId, ChordsReader.ToContentKind(asked));
                return new { chords = Slim.Chords(answer.Header!), kind = asked, summary = Slim.Summary(answer.Content), url, expiresAt };
            },
            BuiltInPermissions.McpIndex),

        new("resolve_chords_link",
            "Find the chordsId of a chords page on the web by its address. state: resolved (with chordsId), notResolved, " +
            "unavailable.",
            McpToolEffect.Read,
            async (
                ClaimsPrincipal user, IndexClient index, SourceCallers callers,
                [Description("http(s) address.")] string url,
                CancellationToken cancellationToken = default) =>
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    throw ShelfException.BadRequest();

                var item = (await index.Sources.Resolve(callers.For(user), [uri], cancellationToken)).Items.Single();
                return new
                {
                    state = item.State switch
                    {
                        ResolveState.Resolved => "resolved",
                        ResolveState.NotResolved => "notResolved",
                        _ => "unavailable",
                    },
                    chordsId = item.Id,
                };
            },
            BuiltInPermissions.McpIndex),

        new("analyze_chords",
            "The index's analysis of chords: detected key(s) with probability, the stated key, and the top repeating " +
            "progressions (coverage of the song, how many songs share it). analysed=false: not analysed yet.",
            McpToolEffect.Read,
            async (ClaimsPrincipal user, AnalysisToolsService tools, string chordsId, CancellationToken cancellationToken = default) =>
            {
                var analysis = await tools.GetAsync(user, chordsId, cancellationToken);
                return new
                {
                    analysis.State,
                    analysis.Analysed,
                    keys = analysis.Keys.Select(x => new { key = x.Key.Name, confidence = Math.Round(x.Confidence, 2) }).ToList(),
                    statedKey = analysis.StatedKey?.Name,
                    loops = analysis.Loops.Take(5).Select(x => new { x.Length, coverage = Math.Round(x.Coverage, 2), x.SongsWithIt }).ToList(),
                };
            },
            BuiltInPermissions.McpIndex),

        new("find_chords_by_progression",
            "Find songs whose chords contain a progression, e.g. 'Am F C G': chords items with coverage. Paged from 1.",
            McpToolEffect.Read,
            async (
                ClaimsPrincipal user, AnalysisToolsService tools,
                [Description("Chord names separated by spaces.")] string progression,
                [Description("From 1, default 1.")] int? page = null,
                [Description("1-25, default 10.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                var similar = await tools.GetSimilarAsync(user, progression, page ?? 1, cancellationToken);
                return new
                {
                    similar.Page,
                    similar.TotalPages,
                    items = Slim.Paged(similar.Items, 0, limit, x => new { chords = Slim.Chords(x.Header), coverage = Math.Round(x.Coverage, 2) }).Items,
                };
            },
            BuiltInPermissions.McpIndex),

        new("get_chords_media",
            "Links (valid minutes, until expiresAt) to play an analysed item's recording, its separate instruments and its " +
            "MIDI notes. Error no_media when it has none.",
            McpToolEffect.Read,
            async (ClaimsPrincipal user, MediaService media, McpAddresses addresses, string chordsId, CancellationToken cancellationToken = default) =>
            {
                var signed = await media.SignChordsAsync(user, chordsId, null, cancellationToken);
                return signed with
                {
                    RecordingUrl = signed.RecordingUrl == null ? null : addresses.Url(signed.RecordingUrl),
                    MidiUrl = signed.MidiUrl == null ? null : addresses.Url(signed.MidiUrl),
                    Stems = signed.Stems.Select(x => x with { Url = addresses.Url(x.Url) }).ToList(),
                };
            },
            BuiltInPermissions.McpIndex),
    ];
}
