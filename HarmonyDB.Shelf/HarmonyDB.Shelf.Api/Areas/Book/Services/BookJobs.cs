using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Clients.Pdfs;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Api.Foundation.SignIn;
using Microsoft.Extensions.Options;
using HarmonyDB.Shelf.Model.Book;
using HarmonyDB.Shelf.Model.Jobs;

namespace HarmonyDB.Shelf.Api.Areas.Book.Services;

/// <summary>
/// Starts the jobs of books: counting each item's pages (to lay the book out) and making the book.
/// </summary>
public sealed class BookService(JobQueue jobs, SourceCallers callers)
{
    public const string PagesJobKind = "book_pages";

    public const string BookJobKind = "book";

    public const int MaxItems = 500;

    public Task<JobInfo> StartPagesAsync(ClaimsPrincipal user, BookRequest request, CancellationToken cancellationToken)
        => StartAsync(user, PagesJobKind, request, cancellationToken);

    public Task<JobInfo> StartBookAsync(ClaimsPrincipal user, BookRequest request, CancellationToken cancellationToken)
        => StartAsync(user, BookJobKind, request, cancellationToken);

    private async Task<JobInfo> StartAsync(ClaimsPrincipal user, string kind, BookRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var input = new BookJobInput(request with { Options = request.Options with { Caption = Caption(request.Options.Caption) } }, callers.For(user).Keys);
        return await jobs.EnqueueAsync(user.GetAccountId()!.Value, kind, request.PlaylistId?.ToString(), input, cancellationToken);
    }

    internal static void Validate(BookRequest request)
    {
        if (request.Items is not { Count: > 0 } || request.Items.Count > MaxItems || request.Options == null!)
            throw ShelfException.BadRequest(BookErrorCodes.BadItems, new Dictionary<string, string> { ["max"] = MaxItems.ToString() });

        if (request.Items.Any(x => string.IsNullOrWhiteSpace(x?.ChordsId) || x.Transpose is < -11 or > 11 || x.Number is < 1))
            throw ShelfException.BadRequest(BookErrorCodes.BadItem);
    }

    private static string? Caption(string? caption) => string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
}

/// <param name="Keys">The custom keys of the person when they asked: the chords are read for them as then.</param>
internal sealed record BookJobInput(BookRequest Request, IReadOnlyList<string> Keys);

/// <summary>
/// Counts the pages of each item, in one column and in two. Stages: <c>reading</c>, <c>printing</c>.
/// </summary>
public sealed class BookPagesJob(BookPrinter printer) : IJobHandler
{
    public async Task<JobCompletion> RunAsync(JobRun run, CancellationToken cancellationToken)
    {
        var input = run.Input<BookJobInput>();
        var count = input.Request.Items.Count;
        var items = await printer.PrepareAsync(run.AccountId, input.Request, input.Keys,
            done => run.ReportAsync("reading", (double)done / count / 2, cancellationToken), cancellationToken);

        var printed = 0;
        var pages = await BookPrinter.ForEachAsync(items, async (item, token) =>
        {
            if (item.State != BookItemState.Printed)
                return new BookItemPages { ChordsId = item.Item.ChordsId, State = item.State, Transpose = item.Transpose };

            var one = await printer.PrintAsync(item, false, token);
            var two = await printer.PrintAsync(item, true, token);
            await run.ReportAsync("printing", 0.5 + (double)Interlocked.Increment(ref printed) / count / 2, token);
            return new BookItemPages
            {
                ChordsId = item.Item.ChordsId,
                State = item.State,
                Transpose = item.Transpose,
                PagesOneColumn = one.PageCount,
                PagesTwoColumns = two.PageCount,
            };
        }, cancellationToken);

        return new(new BookPagesResult { Items = pages });
    }
}

/// <summary>
/// Makes the book: the PDF is the job's file. The QR code on its cover leads to the shelf (its public origin). Stages: <c>reading</c>, <c>printing</c>, <c>binding</c>.
/// </summary>
public sealed class BookJob(BookPrinter printer, PdfsClient pdfs, IOptionsMonitor<SignInOptions> signIn, TimeProvider time) : IJobHandler
{
    public async Task<JobCompletion> RunAsync(JobRun run, CancellationToken cancellationToken)
    {
        var input = run.Input<BookJobInput>();
        var request = input.Request;
        var count = request.Items.Count;
        var items = await printer.PrepareAsync(run.AccountId, request, input.Keys,
            done => run.ReportAsync("reading", (double)done / count / 2, cancellationToken), cancellationToken);

        var numbers = Numbers(items, request.Options.Renumber);
        var songs = items.Select((x, i) => (Item: x, Number: numbers[i])).Where(x => x.Number != null).Select(x => (x.Item, Number: x.Number!.Value)).ToList();
        if (songs.Count == 0)
            throw new JobFailedException(BookErrorCodes.NothingToPrint);

        var printed = 0;
        var pdfsOfSongs = await BookPrinter.ForEachAsync(songs, async (song, token) =>
        {
            var pdf = await printer.PrintAsync(song.Item, song.Item.Item.TwoColumns, token);
            await run.ReportAsync("printing", 0.5 + (double)Interlocked.Increment(ref printed) / songs.Count / 2, token);
            return pdf;
        }, cancellationToken);

        await run.ReportAsync("binding", 1, cancellationToken);
        var caption = request.Options.Caption;
        MadePdf book;
        if (caption == null && songs.Count == 1)
        {
            book = pdfsOfSongs.Single();
        }
        else
        {
            var documents = new List<(byte[] Pdf, int? Number)>();
            if (caption != null && request.Options.Contents)
                documents.Add(((await printer.PrintContentsAsync(songs, cancellationToken)).Data, null));
            documents.AddRange(pdfsOfSongs.Zip(songs, (pdf, song) => (pdf.Data, (int?)song.Number)));
            book = await pdfs.BookAsync(caption, time.GetUtcNow().Year.ToString(), signIn.CurrentValue.PublicOrigin is { } origin ? new Uri(origin) : null, documents, cancellationToken);
        }

        return new(
            new BookResult
            {
                Pages = book.PageCount,
                Items = items.Select((x, i) => new BookItemResult
                {
                    ChordsId = x.Item.ChordsId,
                    State = x.State,
                    Number = numbers[i],
                    Transpose = x.Transpose,
                }).ToList(),
            },
            new(FileName(caption, songs), "application/pdf", book.Data));
    }

    /// <summary>
    /// The number of each item in the book, null for one not printed: 1, 2, 3 … over the printed ones when renumbered;
    /// otherwise each its own number, or its place among the items asked.
    /// </summary>
    internal static IReadOnlyList<int?> Numbers(IReadOnlyList<PreparedItem> items, bool renumber)
    {
        var next = 1;
        return items
            .Select((x, i) => x.State != BookItemState.Printed ? (int?)null : renumber ? next++ : x.Item.Number ?? i + 1)
            .ToList();
    }

    private static string FileName(string? caption, IReadOnlyList<(PreparedItem Item, int Number)> songs)
    {
        var name = caption
                   ?? (songs.Count == 1 ? $"{string.Join(", ", songs[0].Item.Header!.Artists)} - {songs[0].Item.Header!.Title}" : "chords");
        var safe = new string(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim();
        return $"{(safe.Length == 0 ? "chords" : safe)}.pdf";
    }
}
