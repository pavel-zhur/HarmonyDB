using HarmonyDB.Shelf.Api.Foundation.Permissions;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using HarmonyDB.Common;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Common.Transposition;
using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Api.Clients.Pdfs;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Book;
using HarmonyDB.Source.Api.Model.V2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HarmonyDB.Shelf.Api.Areas.Book.Services;

/// <summary>
/// Prints chords for a book: reads each item as the person sees it, in the key the person plays it in (the key asked;
/// else the person's "here" key in the playlist the items come from; else their own key, "mine"; else as written),
/// with the person's way of showing chords (degrees, superscripts, sharps or flats), and makes its PDF page by page as
/// the old shelf did. Each item's PDF is kept for a while by what was printed (<see cref="BookPdfCache"/>), so the same
/// item asked again — counting the pages, then the book — is not made again.
/// </summary>
public sealed class BookPrinter(ShelfDbContext db, IndexClient index, PdfsClient pdfs, BookPdfCache cache)
{
    /// <summary>
    /// How many items are read and printed at once.
    /// </summary>
    private const int Parallelism = 4;

    /// <summary>
    /// The items as the person sees them, and how to print them; in the order asked.
    /// </summary>
    public async Task<IReadOnlyList<PreparedItem>> PrepareAsync(
        Guid accountId, BookRequest request, IReadOnlyList<string> keys, Func<int, Task> progress, CancellationToken cancellationToken)
    {
        var transpositions = await TranspositionsAsync(accountId, request.PlaylistId, request.Items.Select(x => x.ChordsId).ToList(), cancellationToken);
        var settings = await SettingsAsync(accountId, request.Options.Accidentals, cancellationToken);
        var caller = new Caller { Subjects = await AccountSubjects.LoadAsync(db, accountId, cancellationToken), Keys = keys };

        var done = 0;
        return await ForEachAsync(request.Items, async (item, token) =>
        {
            var transpose = item.Transpose ?? transpositions.GetValueOrDefault(item.ChordsId);
            var response = await index.Sources.GetContent(caller, item.ChordsId, [ContentKind.Sheet], null, token);
            await progress(Interlocked.Increment(ref done));
            return response.State switch
            {
                ItemState.Open when response.Content!.Sheet is { } sheet => new PreparedItem(item, BookItemState.Printed, transpose, response.Header, sheet.Root, settings),
                ItemState.Open or ItemState.NotOpen => new PreparedItem(item, BookItemState.NotOpen, transpose, null, null, settings),
                _ => new PreparedItem(item, BookItemState.Unavailable, transpose, null, null, settings),
            };
        }, cancellationToken);
    }

    /// <summary>
    /// The item's PDF, in one column or two.
    /// </summary>
    public Task<MadePdf> PrintAsync(PreparedItem item, bool twoColumns, CancellationToken cancellationToken)
        => ConvertAsync(SongHtml(item, twoColumns), cancellationToken);

    /// <summary>
    /// The page of contents: the songs by their first artist, then title, with their numbers.
    /// </summary>
    public Task<MadePdf> PrintContentsAsync(IReadOnlyList<(PreparedItem Item, int Number)> songs, CancellationToken cancellationToken)
        => ConvertAsync(ContentsHtml(songs), cancellationToken);

    public static async Task<IReadOnlyList<TResult>> ForEachAsync<TItem, TResult>(
        IReadOnlyList<TItem> items, Func<TItem, CancellationToken, Task<TResult>> action, CancellationToken cancellationToken)
    {
        var results = new TResult[items.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, items.Count),
            new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = cancellationToken },
            async (i, token) => results[i] = await action(items[i], token));
        return results;
    }

    private async Task<MadePdf> ConvertAsync(string html, CancellationToken cancellationToken)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(html)));
        if (cache.TryGet(key, out var pdf))
            return pdf;

        pdf = await pdfs.ConvertAsync(html, cancellationToken);
        cache.Set(key, pdf);
        return pdf;
    }

    /// <summary>
    /// Each item's key, by the person's keys: "here" in the playlist over "mine"; the items with neither are left out.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, int>> TranspositionsAsync(Guid accountId, Guid? playlistId, IReadOnlyList<string> chordsIds, CancellationToken cancellationToken)
    {
        var ids = chordsIds.Distinct(StringComparer.Ordinal).ToList();
        var keys = await db.Transpositions
            .Where(x => x.AccountId == accountId && ids.Contains(x.Chords.SourceId) && (x.PlaylistId == null || x.PlaylistId == playlistId))
            .Select(x => new { x.Chords.SourceId, x.PlaylistId, x.Semitones })
            .ToListAsync(cancellationToken);

        return keys
            .GroupBy(x => x.SourceId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.OrderBy(k => k.PlaylistId == null ? 1 : 0).First().Semitones, StringComparer.Ordinal);
    }

    private async Task<PrintSettings> SettingsAsync(Guid accountId, BookAccidentals? accidentals, CancellationToken cancellationToken)
    {
        var settings = await db.AccountSettings.Where(x => x.AccountId == accountId)
            .Select(x => new { x.ShowDegrees, x.ChordSuperscripts, x.Accidentals })
            .SingleOrDefaultAsync(cancellationToken);
        var defaults = new AccountSettings();

        return new(
            settings?.ShowDegrees ?? defaults.ShowDegrees,
            settings?.ChordSuperscripts ?? defaults.ChordSuperscripts,
            accidentals switch
            {
                BookAccidentals.Flats => NoteAlteration.Flat,
                BookAccidentals.Sharps => NoteAlteration.Sharp,
                BookAccidentals.AsWritten => null,
                null => (settings?.Accidentals ?? defaults.Accidentals) switch
                {
                    AccidentalsMode.Flats => NoteAlteration.Flat,
                    AccidentalsMode.Sharps => NoteAlteration.Sharp,
                    _ => null,
                },
                _ => throw new ArgumentOutOfRangeException(nameof(accidentals), accidentals, null),
            });
    }

    /// <summary>
    /// A song's page as the old shelf printed it: the artists and the title, with the source's name, the key and the
    /// sharps or flats; the chords under them.
    /// </summary>
    internal static string SongHtml(PreparedItem item, bool twoColumns)
    {
        var root = item.Root!;
        var settings = new RepresentationSettings(transpose: item.Transpose, alteration: item.Settings.Alteration, isVariableWidth: root.IsVariableWidth)
        {
            ShowDegrees = item.Settings.ShowDegrees,
            ChordSuperscripts = item.Settings.ChordSuperscripts,
            // as the app shows them: with neither sharps nor flats asked for, the notes as they are written (E#, not F)
            ShowNotesAsWritten = item.Settings.Alteration == null,
        };

        var notes = new List<string> { E(item.Header!.Source.Title) };
        if (settings.Transpose != 0)
            notes.Add(settings.Transpose.Transposition());
        if (item.Settings.Alteration is { } alteration)
            notes.Add(alteration == NoteAlteration.Flat ? "b" : "#");

        return Page($"""
                     <h4 class='song-title'>
                         {E(string.Join(", ", item.Header.Artists))} &minus; {E(item.Header.Title ?? string.Empty)} <span style='font-size: 70%; font-weight: normal;'>({string.Join(", ", notes)})</span>
                     </h4>
                     <div class="chords-block {(twoColumns ? "two" : "")}">
                         {root.AsHtml(settings)}
                     </div>
                     """);
    }

    internal static string ContentsHtml(IReadOnlyList<(PreparedItem Item, int Number)> songs)
    {
        var rows = songs
            .Select(x => (artist: x.Item.Header!.Artists.FirstOrDefault() ?? string.Empty, title: x.Item.Header.Title ?? string.Empty, x.Number))
            .OrderBy(x => x.artist, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.title, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => $"<tr><td class='artist'>{E(x.artist)}</td><td class='title'>{E(x.title)}</td><td class='number'>{x.Number}</td></tr>");

        return Page($"""
                     <table class='contents'>
                     {string.Join(Environment.NewLine, rows)}
                     </table>
                     """);
    }

    private static string E(string text) => WebUtility.HtmlEncode(text);

    private static string Page(string body) => $$"""
        <html>
            <head>
                <meta charset="utf-8">
                <style>
        @page {
            size: A4;
            margin: 16mm;
        }

        .two {
            column-count: 2;
            -webkit-column-count: 2;
            -moz-column-count: 2;
        }

        h4.song-title {
            font-family: Open Sans,-apple-system,Roboto,Helvetica neue,Helvetica,sans-serif;
            font-size: 150%;
        }

        pre {
            line-height: 21px;
            font-family: SFMono-Regular,Menlo,Monaco,Consolas,"Liberation Mono","Courier New",monospace;
            font-size: 14px;
        }

        div.chords-block div.pre-variable-width {
            white-space: pre-wrap;
            font-family: Tahoma, Roboto, Arial, Helvetica, sans-serif;
        }

        div.chords-block pre {
            white-space: pre-wrap;
            padding-bottom: 6px;
        }

        div.chords-block .chord {
            background-color: white;
            font-weight: bold;
            padding: 0 3px;
            text-wrap: nowrap;
            white-space: nowrap;
        }

        /* a chord's superscripts, subscripts and degree take no height of their own, as in the app: a taller chord's
           white background would cover the tops of the words under it */
        div.chords-block .chord sub, div.chords-block .chord sup {
            position: relative;
            font-size: .75em;
            line-height: 0;
            vertical-align: baseline;
        }

        div.chords-block .chord sub {
            bottom: -.25em;
        }

        div.chords-block .chord sup {
            top: -.5em;
        }

        div.chords-block .chord-degree {
            margin-left: .25em;
            font-weight: normal;
            color: #666;
        }

        div.chords-block div.bold-div {
            font-weight: bold;
        }

        div.chords-block span.chords-line {
            line-height: inherit;
        }

        div.chords-block div.chords-line {
            background: white;
        }

        div.chords-block div.chords-double-line {
            margin-bottom: 2px;
            page-break-inside: avoid;
        }

        div.chords-block span.chords-double-line {
            line-height: 36px;
            position: relative;
            top: 8px;
            display: inline-block;
        }

        div.chords-block span.flying {
            position: absolute;
        }

        div.chords-block span.flying2 {
            position: relative;
            top: -17px;
        }

        table.contents {
            width: 100%;
            border-collapse: collapse;
            font-family: Open Sans,-apple-system,Roboto,Helvetica neue,Helvetica,sans-serif;
            font-size: 11px;
        }

        table.contents td {
            border-top: 0.5px solid #ccc;
            padding: 1px 4px;
            page-break-inside: avoid;
        }

        table.contents td.artist {
            color: #555;
            width: 40%;
        }

        table.contents td.title {
            font-weight: bold;
        }

        table.contents td.number {
            text-align: right;
            color: #555;
        }
                </style>
            </head>
            <body>
        {{body}}
            </body>
        </html>
        """;
}

/// <param name="Alteration">Sharps or flats; null: as written.</param>
public sealed record PrintSettings(bool ShowDegrees, bool ChordSuperscripts, NoteAlteration? Alteration);

/// <param name="Header">The item's header as the person sees it, when it is printed.</param>
/// <param name="Root">The item's sheet, when it is printed.</param>
public sealed record PreparedItem(BookItem Item, BookItemState State, int Transpose, Header? Header, NodeHtml? Root, PrintSettings Settings);

/// <summary>
/// The PDFs of items printed lately, by what was printed: kept in memory, up to a size, for a day.
/// </summary>
public sealed class BookPdfCache : IDisposable
{
    private const long SizeLimit = 256L * 1024 * 1024;

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = SizeLimit });

    public bool TryGet(string key, out MadePdf pdf) => _cache.TryGetValue(key, out pdf!);

    public void Set(string key, MadePdf pdf)
        => _cache.Set(key, pdf, new MemoryCacheEntryOptions { Size = pdf.Data.Length, SlidingExpiration = TimeSpan.FromDays(1) });

    public void Dispose() => _cache.Dispose();
}
