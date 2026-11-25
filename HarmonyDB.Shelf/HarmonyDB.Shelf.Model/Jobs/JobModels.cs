
namespace HarmonyDB.Shelf.Model.Jobs;

public enum JobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
}

/// <summary>
/// Background work the person started (illustrations, a book, an import …): where it is, and what came of it.
/// </summary>
public record JobInfo
{
    public required Guid Id { get; init; }

    /// <summary>
    /// What work it is: "illustrations", "book", "book_pages", "import" …
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// What it works on (the chords' id, a playlist's id …), when it works on one thing.
    /// </summary>
    public string? SubjectId { get; init; }

    public required JobStatus Status { get; init; }

    /// <summary>
    /// The step it is at, a code the frontend words (each kind has its own).
    /// </summary>
    public string? Stage { get; init; }

    /// <summary>
    /// The part done, 0 … 1, when known.
    /// </summary>
    public double? Progress { get; init; }

    /// <summary>
    /// Why it failed: a code the frontend words.
    /// </summary>
    public string? ErrorCode { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>
    /// What came of it, when it succeeded and its kind ends with a result: one of the results, told apart by its
    /// <c>kind</c> (the job's kind).
    /// </summary>
    public JobResult? Result { get; init; }

    /// <summary>
    /// The file it made, while it is kept: fetched at <c>/api/jobs/{id}/file</c>.
    /// </summary>
    public JobFileInfo? File { get; init; }
}

/// <summary>
/// What a job ended with: each kind of jobs that has a result has its own type of it, given with the kind.
/// </summary>
public abstract record JobResult;

public record JobFileInfo
{
    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required long Size { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

public static class JobsErrorCodes
{
    /// <summary>404: no such job of the person's.</summary>
    public const string NoSuchJob = "no_such_job";

    /// <summary>404: the job made no file, or it is no longer kept.</summary>
    public const string NoFile = "no_job_file";

    /// <summary>429: the person has as many jobs of this kind going as one may (details: <c>max</c>); wait for one to end.</summary>
    public const string TooManyJobs = "too_many_jobs";

    /// <summary>The job was interrupted too many times and is given up.</summary>
    public const string Interrupted = "job_interrupted";
}
