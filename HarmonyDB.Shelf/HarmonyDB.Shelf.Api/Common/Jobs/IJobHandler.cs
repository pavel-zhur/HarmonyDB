using HarmonyDB.Shelf.Model.Jobs;
namespace HarmonyDB.Shelf.Api.Common.Jobs;

/// <summary>
/// Does one kind of background work. Registered with <see cref="JobsServiceCollectionExtensions.AddJobHandler{THandler}"/>
/// under its kind; made anew, in a scope of its own, for every run. A run may be interrupted (the app stops) and run
/// again from the start for the same job: what it does must bear repeating.
/// </summary>
public interface IJobHandler
{
    /// <returns>What came of it. To fail with a code the frontend words, throw <see cref="JobFailedException"/>.</returns>
    Task<JobCompletion> RunAsync(JobRun run, CancellationToken cancellationToken);
}

/// <summary>
/// What a handler ended with: its result (written as JSON), and the file it made, if any; or, made by <see cref="Again"/>,
/// that it is to be run again later.
/// </summary>
public sealed record JobCompletion(JobResult? Result, JobFile? File = null)
{
    /// <summary>
    /// When set, the job is not over: it goes back to the queue and is run again from the start, not before this time.
    /// </summary>
    public DateTimeOffset? RunAgainNotBefore { get; private init; }

    /// <summary>
    /// The job is to be run again, from the start, not before the time (it waits for something; it holds no worker
    /// meanwhile). Such a run does not count as a start (<see cref="JobsOptions.MaxAttempts"/>).
    /// </summary>
    public static JobCompletion Again(DateTimeOffset notBefore) => new((JobResult?)null) { RunAgainNotBefore = notBefore };
}

/// <summary>
/// A file a job made (a PDF …): kept with the job until it expires.
/// </summary>
public sealed record JobFile(string FileName, string ContentType, byte[] Data);

/// <summary>
/// The job fails with a stable code the frontend words; nothing is logged as an error.
/// </summary>
public sealed class JobFailedException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
