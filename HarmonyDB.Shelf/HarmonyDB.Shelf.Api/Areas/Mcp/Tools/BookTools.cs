using System.ComponentModel;
using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Book.Services;
using HarmonyDB.Shelf.Api.Areas.Mcp.Services;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Book;
using HarmonyDB.Shelf.Model.Jobs;

namespace HarmonyDB.Shelf.Api.Areas.Mcp.Tools;

/// <summary>
/// The PDF book, and the person's jobs (the book, imports …) with the links to their files.
/// </summary>
public static class BookTools
{
    public static IEnumerable<McpTool> All() =>
    [
        new("make_book",
            "Make a PDF book of chords in the given order, each in this person's key for it (their key 'here' when " +
            "playlistId is given) unless transpose is given; with a caption: a cover, numbers, optional contents. A job: " +
            "get_job gives the PDF link when done.",
            McpToolEffect.Add,
            async (
                ClaimsPrincipal user, BookService books,
                [Description("chordsIds in order, 1-500.")] string[] chordsIds,
                [Description("Their playlist, for keys 'here'.")] Guid? playlistId = null,
                [Description("Cover title.")] string? caption = null,
                [Description("Contents page (with a caption).")] bool? contents = null,
                BookAccidentals? accidentals = null,
                [Description("Semitones -11..11 for all items.")] int? transpose = null,
                [Description("Two columns for long songs.")] bool? twoColumns = null,
                CancellationToken cancellationToken = default)
                => Job(await books.StartBookAsync(user, new()
                {
                    Items = (chordsIds ?? []).Select(x => new BookItem { ChordsId = x, Transpose = transpose, TwoColumns = twoColumns ?? false }).ToList(),
                    PlaylistId = playlistId,
                    Options = new() { Caption = caption, Contents = contents ?? false, Renumber = true, Accidentals = accidentals },
                }, cancellationToken)),
            BuiltInPermissions.McpPdf),

        new("list_jobs",
            "This person's jobs (books, imports …), queued, running or finished lately, newest first.",
            McpToolEffect.Read,
            async (
                ClaimsPrincipal user, JobQueue jobs,
                [Description("A kind: book, import …")] string? kind = null,
                int? offset = null,
                [Description("1-25, default 10.")] int? limit = null,
                CancellationToken cancellationToken = default)
                => Slim.Paged(await jobs.ListAsync(user.Me(), kind, cancellationToken), offset, limit, Job))
        {
            AnyOf = [BuiltInPermissions.McpPdf, BuiltInPermissions.McpLibraryWrite],
        },

        new("get_job",
            "A job's state (queued, running, succeeded, failed), stage, error code, result, and fileUrl — a download link " +
            "valid a few minutes — when it made a file. Ask again after a few seconds while it runs.",
            McpToolEffect.Read,
            async (ClaimsPrincipal user, JobQueue jobs, McpFiles files, Guid jobId, CancellationToken cancellationToken = default) =>
            {
                var job = await jobs.GetAsync(user.Me(), jobId, cancellationToken) ?? throw ShelfException.NotFound(JobsErrorCodes.NoSuchJob);
                var link = job.File == null ? ((string Url, DateTimeOffset ExpiresAt)?)null : files.JobFile(user.Me(), job.Id);
                // the result as its own type: the tools' JSON does not tell the results apart by their kind
                return new { job = Job(job), result = (object?)job.Result, fileUrl = link?.Url, fileUrlExpiresAt = link?.ExpiresAt };
            })
        {
            AnyOf = [BuiltInPermissions.McpPdf, BuiltInPermissions.McpLibraryWrite],
        },
    ];

    public static object Job(JobInfo x) => new { jobId = x.Id, x.Kind, x.Status, x.Stage, x.Progress, x.ErrorCode, x.CreatedAt, x.FinishedAt };
}
