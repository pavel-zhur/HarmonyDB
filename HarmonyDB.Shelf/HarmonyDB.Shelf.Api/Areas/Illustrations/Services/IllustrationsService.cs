using System.Security.Claims;
using System.Text.Json;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Clients.Illustrations;
using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Illustrations;
using HarmonyDB.Source.Api.Model.V2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Illustrations.Services;

/// <summary>
/// The illustrations of chords: they belong to the chords (by their id), are the same for everyone, and are drawn on
/// the server by a preset, a batch at a time, as a job (from half a minute to two). One batch is drawn for chords at a
/// time; ordering while one is drawn gives that one. A person draws at most <see cref="IllustrationsOptions.DailyImageLimit"/>
/// pictures in a day, as in the old shelf.
/// </summary>
public sealed class IllustrationsService(
    ShelfDbContext db,
    IllustrationsClient illustrations,
    IndexClient index,
    SourceCallers callers,
    JobQueue jobs,
    IOptionsMonitor<IllustrationsOptions> options,
    TimeProvider time)
{
    public const string JobKind = "illustrations";

    public const string ImagesPath = "/api/illustrations/images";

    public async Task<IllustrationPresetList> GetPresetsAsync(CancellationToken cancellationToken)
        => new() { Presets = (await illustrations.GetPresetsAsync(cancellationToken)).Presets };

    /// <summary>
    /// The chords' illustrations, when the chords are open to the caller; and the batch being drawn, if one is.
    /// </summary>
    public async Task<ChordsIllustrations> GetAsync(ClaimsPrincipal user, string chordsId, CancellationToken cancellationToken)
    {
        await RequireOpenAsync(callers.For(user), chordsId, cancellationToken);

        var sizes = (await illustrations.GetPresetsAsync(cancellationToken)).Sizes;
        var images = await illustrations.ReadAsync(chordsId, cancellationToken);
        var drawing = await db.Jobs
            .Where(x => x.Kind == JobKind && x.SubjectId == chordsId && (x.State == JobState.Queued || x.State == JobState.Running))
            .OrderBy(x => x.CreatedAt)
            .Select(x => new { x.Id, x.AccountId, x.Input })
            .FirstOrDefaultAsync(cancellationToken);
        var me = user.GetAccountId();

        return new()
        {
            ChordsId = chordsId,
            Images = images.Select(x => new IllustrationImage
            {
                Id = x.Id,
                Preset = x.Preset,
                CreatedAt = x.CreatedOn,
                Urls = sizes.ToDictionary(s => s, s => $"{ImagesPath}/{x.Id}/{s}"),
            }).ToList(),
            Drawing = drawing == null
                ? null
                : new()
                {
                    Preset = JsonSerializer.Deserialize<DrawInput>(drawing.Input, JobQueue.JsonOptions)!.Preset,
                    JobId = drawing.AccountId == me ? drawing.Id : null,
                },
        };
    }

    /// <summary>
    /// Orders a batch drawn by the preset, as a job; while a batch is drawn for the chords, that one's job.
    /// </summary>
    public async Task<Guid> DrawAsync(ClaimsPrincipal user, string chordsId, string preset, CancellationToken cancellationToken)
    {
        if (!(await illustrations.GetPresetsAsync(cancellationToken)).Presets.Contains(preset))
            throw ShelfException.BadRequest(IllustrationsErrorCodes.UnknownPreset);

        var caller = callers.For(user);
        await RequireOpenAsync(caller, chordsId, cancellationToken);

        // one batch for the chords at a time: two orders at once wait for each other here
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({JobKind + ":" + chordsId}))", cancellationToken);

        if (await jobs.FindActiveAsync(JobKind, chordsId, cancellationToken) is { } active)
            return active.Id;

        var me = user.GetAccountId()!.Value;
        var used = await UsedTodayAsync(me, cancellationToken);
        var limit = options.CurrentValue.DailyImageLimit;
        if (used >= limit)
        {
            throw new ShelfException(StatusCodes.Status429TooManyRequests, IllustrationsErrorCodes.DailyLimit, new Dictionary<string, string>
            {
                ["limit"] = limit.ToString(),
                ["used"] = used.ToString(),
            });
        }

        var job = await jobs.EnqueueAsync(me, JobKind, chordsId, new DrawInput(chordsId, preset, caller.Keys), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return job.Id;
    }

    /// <summary>
    /// The most pictures one batch draws (the attempt drawn has at most this many prompts, as the old shelf chose it).
    /// </summary>
    public const int MaxBatchImages = 7;

    /// <summary>
    /// The pictures the person's batches of the last day take: those drawn by the batches that ended (also by those
    /// that failed after drawing some), and a whole batch for each one queued or running.
    /// </summary>
    internal async Task<int> UsedTodayAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var since = time.GetUtcNow().AddDays(-1);
        var jobs = await db.Jobs
            .Where(x => x.AccountId == accountId && x.Kind == JobKind && (x.FinishedAt == null || x.FinishedAt >= since))
            .Select(x => new { x.State, x.Result, x.Input, x.StartedAt, x.FinishedAt })
            .ToListAsync(cancellationToken);

        var used = 0;
        foreach (var job in jobs)
        {
            switch (job.State)
            {
                case JobState.Queued or JobState.Running:
                    used += MaxBatchImages;
                    break;
                case JobState.Succeeded:
                    used += JsonSerializer.Deserialize<IllustrationsJobResult>(job.Result!, JobQueue.JsonOptions)!.Made;
                    break;
                case JobState.Failed when job.StartedAt != null:
                    // what the service kept of the batch while the job ran: one batch of a preset for chords at a time
                    var input = JsonSerializer.Deserialize<DrawInput>(job.Input, JobQueue.JsonOptions)!;
                    used += (await illustrations.ReadAsync(input.ChordsId, cancellationToken))
                        .Count(x => x.Preset == input.Preset && x.CreatedOn >= job.StartedAt && x.CreatedOn <= job.FinishedAt);
                    break;
            }
        }

        return used;
    }

    private async Task RequireOpenAsync(Caller caller, string chordsId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(chordsId))
            throw ShelfException.BadRequest();

        switch ((await index.Sources.GetHeaders(caller, [chordsId], cancellationToken)).Headers.Single().State)
        {
            case ItemState.Open:
                return;
            case ItemState.NotOpen:
                throw ShelfException.NotFound(IllustrationsErrorCodes.ChordsNotOpen);
            default:
                throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
        }
    }

    /// <param name="Keys">The custom keys of the person when they ordered it: the chords are read for them as then.</param>
    internal sealed record DrawInput(string ChordsId, string Preset, IReadOnlyList<string> Keys);
}

/// <summary>
/// Draws a batch: reads the chords' words as the person who ordered it, and has the illustrations service draw by them.
/// Stages: <c>reading</c>, <c>drawing</c>.
/// </summary>
public sealed class IllustrationsJob(IllustrationsClient illustrations, IndexClient index, SourceCallers callers) : IJobHandler
{
    public async Task<JobCompletion> RunAsync(JobRun run, CancellationToken cancellationToken)
    {
        var input = run.Input<IllustrationsService.DrawInput>();
        await run.ReportAsync("reading", cancellationToken: cancellationToken);

        var content = await index.Sources.GetContent(await callers.ForAccountAsync(run.AccountId, input.Keys, cancellationToken), input.ChordsId, [ContentKind.Sheet], null, cancellationToken);
        if (content.State != ItemState.Open)
            throw new JobFailedException(content.State == ItemState.NotOpen ? IllustrationsErrorCodes.ChordsNotOpen : ErrorCodes.Unavailable);

        var lyrics = content.Content!.Sheet?.Root.AsLyrics() ?? string.Empty;
        await run.ReportAsync("drawing", cancellationToken: cancellationToken);
        var answer = await illustrations.GenerateAsync(input.ChordsId, lyrics, input.Preset, $"shelf account {run.AccountId}", cancellationToken);
        return answer.Outcome switch
        {
            GenerateOutcome.Made or GenerateOutcome.AlreadyMade => new(new IllustrationsJobResult { Made = answer.Made }),
            GenerateOutcome.NoLyrics => throw new JobFailedException(IllustrationsErrorCodes.NoLyrics),
            GenerateOutcome.NoPrompts => throw new JobFailedException(IllustrationsErrorCodes.NothingToDraw),
            _ => throw new ArgumentOutOfRangeException(nameof(run), answer.Outcome, null),
        };
    }
}

/// <summary>
/// The configuration section <c>Illustrations</c>.
/// </summary>
public sealed class IllustrationsOptions
{
    public const string SectionName = "Illustrations";

    /// <summary>
    /// How many pictures a person may have drawn in a day.
    /// </summary>
    public int DailyImageLimit { get; set; } = 42;
}
