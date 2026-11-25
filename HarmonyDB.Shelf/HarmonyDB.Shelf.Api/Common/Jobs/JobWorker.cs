using System.Text.Json;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Common.Jobs;

/// <summary>
/// Wakes the worker when a job is queued in this copy of the app; other copies find it by looking at the queue.
/// </summary>
public sealed class JobSignal
{
    private TaskCompletionSource _next = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Notify() => Interlocked.Exchange(ref _next, new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();

    internal Task Next => Volatile.Read(ref _next).Task;
}

/// <summary>
/// The kinds of jobs and their handlers' types.
/// </summary>
internal sealed record JobHandlerRegistration(string Kind, Type HandlerType);

/// <summary>
/// Runs the queued jobs of the kinds this app has handlers for, up to <see cref="JobsOptions.Concurrency"/> at once.
/// A job is taken under a row lock that other copies skip, and held by a lease renewed while it runs; a job whose
/// lease ran out (its copy stopped) is taken again, up to <see cref="JobsOptions.MaxAttempts"/> starts. A job stopped
/// with the app goes back to the queue at once; one that asked to be run again later goes back with its time.
/// Finished jobs' files and old jobs are deleted in time.
/// </summary>
internal sealed class JobWorker(
    IServiceScopeFactory scopes,
    IEnumerable<JobHandlerRegistration> registrations,
    JobSignal signal,
    IOptionsMonitor<JobsOptions> options,
    TimeProvider time,
    ILogger<JobWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);

    private readonly IReadOnlyDictionary<string, Type> _handlers = registrations.ToDictionary(x => x.Kind, x => x.HandlerType);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_handlers.Count == 0)
            return;

        var running = new List<Task>();
        var cleanedAt = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            running.RemoveAll(x => x.IsCompleted);
            var next = signal.Next;
            var tookOne = false;
            try
            {
                if (running.Count < options.CurrentValue.Concurrency && await ClaimAsync(stoppingToken) is { } claim)
                {
                    tookOne = true;
                    if (claim.Job != null)
                        running.Add(RunAsync(claim.Job, stoppingToken));
                }

                if (time.GetUtcNow() - cleanedAt > CleanupInterval)
                {
                    await CleanUpAsync(stoppingToken);
                    cleanedAt = time.GetUtcNow();
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "The job queue could not be read.");
            }

            if (tookOne)
                continue;

            try
            {
                await Task.WhenAny(running.Append(next).Append(Task.Delay(options.CurrentValue.Poll, time, stoppingToken)));
            }
            catch (OperationCanceledException)
            {
            }
        }

        await Task.WhenAll(running);
    }

    /// <summary>
    /// Takes the oldest job that is queued, or running with its lease run out. Null when there is none; a claim without a
    /// job when the job taken was given up.
    /// </summary>
    private async Task<Claim?> ClaimAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfDbContext>();
        var now = time.GetUtcNow();
        var kinds = _handlers.Keys.ToArray();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var job = await db.Jobs
            .FromSql($"""
                      SELECT * FROM jobs
                      WHERE kind = ANY({kinds})
                        AND ((state = 'queued' AND (not_before IS NULL OR not_before <= {now})) OR (state = 'running' AND locked_until < {now}))
                      ORDER BY created_at
                      LIMIT 1
                      FOR UPDATE SKIP LOCKED
                      """)
            .SingleOrDefaultAsync(cancellationToken);
        if (job == null)
            return null;

        if (job.Attempts >= options.CurrentValue.MaxAttempts)
        {
            logger.LogWarning("The job {JobId} ({Kind}) was interrupted {Attempts} times and is given up.", job.Id, job.Kind, job.Attempts);
            job.State = JobState.Failed;
            job.ErrorCode = JobsErrorCodes.Interrupted;
            job.FinishedAt = now;
            job.LockedUntil = null;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(null);
        }

        job.Attempts++;
        job.State = JobState.Running;
        job.StartedAt ??= now;
        job.LockedUntil = now + options.CurrentValue.Lease;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(job);
    }

    private async Task RunAsync(Job job, CancellationToken stoppingToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeat = HeartbeatAsync(job, stop.Token);
        try
        {
            JobCompletion completion;
            await using (var scope = scopes.CreateAsyncScope())
            {
                var handler = (IJobHandler)scope.ServiceProvider.GetRequiredService(_handlers[job.Kind]);
                completion = await handler.RunAsync(new(job, scopes), stop.Token);
            }

            if (completion.RunAgainNotBefore is { } notBefore)
                await RequeueAsync(job, notBefore);
            else
                await FinishAsync(job, JobState.Succeeded, completion, null);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await ReleaseAsync(job);
        }
        catch (JobFailedException exception)
        {
            await FinishAsync(job, JobState.Failed, null, exception.Code);
        }
        catch (ShelfException exception)
        {
            // what a request would answer with a code (a service that did not answer …) ends the job with that code
            logger.LogWarning("The job {JobId} ({Kind}) failed: {Code}.", job.Id, job.Kind, exception.Code);
            await FinishAsync(job, JobState.Failed, null, exception.Code);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The job {JobId} ({Kind}) failed.", job.Id, job.Kind);
            await FinishAsync(job, JobState.Failed, null, ErrorCodes.InternalError);
        }
        finally
        {
            await stop.CancelAsync();
            await heartbeat;
        }
    }

    /// <summary>
    /// Renews the lease while the job runs.
    /// </summary>
    private async Task HeartbeatAsync(Job job, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var lease = options.CurrentValue.Lease;
            try
            {
                await Task.Delay(lease / 3, time, cancellationToken);
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ShelfDbContext>();
                var until = time.GetUtcNow() + lease;
                await db.Jobs
                    .Where(x => x.Id == job.Id && x.State == JobState.Running && x.Attempts == job.Attempts)
                    .ExecuteUpdateAsync(x => x.SetProperty(j => j.LockedUntil, until), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // tried again at the next beat: one failure must not let a running job be taken again
                logger.LogError(exception, "The lease of the job {JobId} could not be renewed this time.", job.Id);
            }
        }
    }

    /// <summary>
    /// Writes how the job ended, unless another start of it has taken it meanwhile.
    /// </summary>
    private async Task FinishAsync(Job job, JobState state, JobCompletion? completion, string? errorCode)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ShelfDbContext>();
            var row = await db.Jobs.SingleOrDefaultAsync(x => x.Id == job.Id && x.State == JobState.Running && x.Attempts == job.Attempts);
            if (row == null)
            {
                logger.LogWarning("The job {JobId} ({Kind}) was taken by another start before it ended here.", job.Id, job.Kind);
                return;
            }

            var now = time.GetUtcNow();
            row.State = state;
            row.ErrorCode = errorCode;
            row.FinishedAt = now;
            row.LockedUntil = null;
            if (state == JobState.Succeeded)
            {
                row.Progress = 1;
                row.Result = completion!.Result == null ? null : JsonSerializer.Serialize(completion.Result, JobQueue.JsonOptions);
                if (completion.File is { } file)
                {
                    db.JobOutputs.Add(new()
                    {
                        JobId = row.Id,
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        Data = file.Data,
                        CreatedAt = now,
                        ExpiresAt = now + options.CurrentValue.FileLifetime,
                    });
                }
            }

            await db.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "How the job {JobId} ended could not be written.", job.Id);
        }
    }

    /// <summary>
    /// Puts a job that asked to be run again later back to the queue, not to be taken before the time; the start does not
    /// count.
    /// </summary>
    private async Task RequeueAsync(Job job, DateTimeOffset notBefore)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ShelfDbContext>();
            await db.Jobs
                .Where(x => x.Id == job.Id && x.State == JobState.Running && x.Attempts == job.Attempts)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(j => j.State, JobState.Queued)
                    .SetProperty(j => j.Attempts, j => j.Attempts - 1)
                    .SetProperty(j => j.NotBefore, notBefore)
                    .SetProperty(j => j.LockedUntil, (DateTimeOffset?)null));
        }
        catch (Exception exception)
        {
            // its lease runs out and it is taken again then
            logger.LogError(exception, "The job {JobId} could not be put back to the queue for later.", job.Id);
        }
    }

    /// <summary>
    /// Puts a job stopped with the app back to the queue: the start does not count.
    /// </summary>
    private async Task ReleaseAsync(Job job)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ShelfDbContext>();
            await db.Jobs
                .Where(x => x.Id == job.Id && x.State == JobState.Running && x.Attempts == job.Attempts)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(j => j.State, JobState.Queued)
                    .SetProperty(j => j.Attempts, j => j.Attempts - 1)
                    .SetProperty(j => j.LockedUntil, (DateTimeOffset?)null), timeout.Token);
        }
        catch (Exception exception)
        {
            // its lease runs out and it is taken again then
            logger.LogWarning(exception, "The job {JobId} could not be put back to the queue.", job.Id);
        }
    }

    private async Task CleanUpAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfDbContext>();
        var now = time.GetUtcNow();
        var before = now - options.CurrentValue.Retention;
        await db.JobOutputs.Where(x => x.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
        await db.Jobs.Where(x => x.FinishedAt != null && x.FinishedAt < before).ExecuteDeleteAsync(cancellationToken);
    }

    private sealed record Claim(Job? Job);
}
