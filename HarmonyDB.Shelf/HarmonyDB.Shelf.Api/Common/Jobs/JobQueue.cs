using System.Text.Json;
using HarmonyDB.Shelf.Api.Foundation;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Common.Jobs;

/// <summary>
/// Starts background work for a person and tells them where it is. The work is done by <see cref="JobWorker"/> with the
/// handler registered for the job's kind.
/// </summary>
public sealed class JobQueue(ShelfDbContext db, JobSignal signal, TimeProvider time, IOptionsMonitor<JobsOptions> options)
{
    /// <summary>
    /// The JSON of the jobs' inputs and results: the browser API's.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    /// <summary>
    /// Queues a job and saves it (within the caller's transaction, if one is open).
    /// </summary>
    /// <param name="kind">The kind a handler is registered for.</param>
    /// <param name="subjectId">What it works on, to find the work going on for a thing (<see cref="FindActiveAsync"/>).</param>
    /// <param name="input">What it is asked to do; given back to the handler by <see cref="JobRun.Input{T}"/>.</param>
    /// <exception cref="ShelfException">429 <c>too_many_jobs</c>: the person has <see cref="JobsOptions.MaxActivePerPerson"/>
    /// jobs of this kind queued or running already (details: <c>max</c>).</exception>
    public async Task<JobInfo> EnqueueAsync(Guid accountId, string kind, string? subjectId, object input, CancellationToken cancellationToken)
    {
        // the person's jobs of the kind are counted and added one request at a time: two at once wait for each other here,
        // in the caller's transaction when it has one
        await using var own = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({"jobs:" + accountId + ":" + kind}))", cancellationToken);

        var max = options.CurrentValue.MaxActivePerPerson;
        if (await db.Jobs.CountAsync(x => x.AccountId == accountId && x.Kind == kind && (x.State == JobState.Queued || x.State == JobState.Running), cancellationToken) >= max)
            throw new ShelfException(StatusCodes.Status429TooManyRequests, JobsErrorCodes.TooManyJobs, new Dictionary<string, string> { ["max"] = max.ToString() });

        var job = new Job
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Kind = kind,
            SubjectId = subjectId,
            State = JobState.Queued,
            Input = JsonSerializer.Serialize(input, JsonOptions),
            CreatedAt = time.GetUtcNow(),
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        if (own != null)
            await own.CommitAsync(cancellationToken);

        signal.Notify();
        return ToInfo(job, null);
    }

    /// <summary>
    /// The person's job.
    /// </summary>
    public async Task<JobInfo?> GetAsync(Guid accountId, Guid jobId, CancellationToken cancellationToken)
        => (await Query(db.Jobs.Where(x => x.Id == jobId && x.AccountId == accountId)).SingleOrDefaultAsync(cancellationToken)) is { } row
            ? ToInfo(row.Job, row.File)
            : null;

    /// <summary>
    /// The person's jobs that are queued or running, and those finished within <see cref="JobsOptions.RecentTime"/>,
    /// newest first; of one kind, when it is given.
    /// </summary>
    public async Task<IReadOnlyList<JobInfo>> ListAsync(Guid accountId, string? kind, CancellationToken cancellationToken)
    {
        var since = time.GetUtcNow() - options.CurrentValue.RecentTime;
        var jobs = db.Jobs.Where(x => x.AccountId == accountId && (x.FinishedAt == null || x.FinishedAt >= since));
        if (kind != null)
            jobs = jobs.Where(x => x.Kind == kind);

        return (await Query(jobs.OrderByDescending(x => x.CreatedAt).Take(options.CurrentValue.ListLimit)).ToListAsync(cancellationToken))
            .Select(x => ToInfo(x.Job, x.File))
            .ToList();
    }

    /// <summary>
    /// The job of this kind queued or running for this thing, whoever started it, the earliest first.
    /// </summary>
    public async Task<JobInfo?> FindActiveAsync(string kind, string subjectId, CancellationToken cancellationToken)
        => (await Query(db.Jobs
                    .Where(x => x.Kind == kind && x.SubjectId == subjectId && (x.State == JobState.Queued || x.State == JobState.Running))
                    .OrderBy(x => x.CreatedAt))
                .FirstOrDefaultAsync(cancellationToken)) is { } row
            ? ToInfo(row.Job, row.File)
            : null;

    /// <summary>
    /// The file the person's job made, while it is kept.
    /// </summary>
    public async Task<JobFile?> GetFileAsync(Guid accountId, Guid jobId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        return await db.JobOutputs
            .Where(x => x.JobId == jobId && x.Job.AccountId == accountId && x.ExpiresAt > now)
            .Select(x => new JobFile(x.FileName, x.ContentType, x.Data))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private IQueryable<JobRow> Query(IQueryable<Job> jobs)
    {
        var now = time.GetUtcNow();
        return jobs.Select(x => new JobRow(
            x,
            x.Output != null && x.Output.ExpiresAt > now
                ? new JobFileInfo { FileName = x.Output.FileName, ContentType = x.Output.ContentType, Size = x.Output.Data.Length, ExpiresAt = x.Output.ExpiresAt }
                : null));
    }

    internal static JobInfo ToInfo(Job job, JobFileInfo? file) => new()
    {
        Id = job.Id,
        Kind = job.Kind,
        SubjectId = job.SubjectId,
        Status = job.State switch
        {
            JobState.Queued => JobStatus.Queued,
            JobState.Running => JobStatus.Running,
            JobState.Succeeded => JobStatus.Succeeded,
            JobState.Failed => JobStatus.Failed,
            JobState.Cancelled => JobStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(job), job.State, null),
        },
        Stage = job.Stage,
        Progress = job.Progress,
        ErrorCode = job.ErrorCode,
        CreatedAt = job.CreatedAt,
        StartedAt = job.StartedAt,
        FinishedAt = job.FinishedAt,
        Result = job.Result == null ? null : JsonDocument.Parse(job.Result).RootElement.Clone(),
        File = file,
    };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        FoundationExtensions.ConfigureJson(options);
        return options;
    }

    private sealed record JobRow(Job Job, JobFileInfo? File);
}
