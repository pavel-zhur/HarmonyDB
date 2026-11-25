using System.Text.Json;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Common.Jobs;

/// <summary>
/// One run of a job, as its handler sees it: who started it, what it was asked, and a way to tell how far it is.
/// </summary>
public sealed class JobRun
{
    private readonly IServiceScopeFactory _scopes;
    private readonly string _input;

    internal JobRun(Job job, IServiceScopeFactory scopes)
    {
        Id = job.Id;
        AccountId = job.AccountId;
        Kind = job.Kind;
        SubjectId = job.SubjectId;
        Attempt = job.Attempts;
        CreatedAt = job.CreatedAt;
        _input = job.Input;
        _scopes = scopes;
    }

    public Guid Id { get; }

    /// <summary>
    /// The person who started it.
    /// </summary>
    public Guid AccountId { get; }

    public string Kind { get; }

    public string? SubjectId { get; }

    /// <summary>
    /// When it was queued first.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// 1 for the first run; more when an interrupted run is run again.
    /// </summary>
    public int Attempt { get; }

    /// <summary>
    /// What it was asked to do, as given to <see cref="JobQueue.EnqueueAsync"/>.
    /// </summary>
    public T Input<T>() => JsonSerializer.Deserialize<T>(_input, JobQueue.JsonOptions)!;

    /// <summary>
    /// Tells the person how far it is: the step (a code the frontend words) and, when known, the part done (0 … 1).
    /// </summary>
    public async Task ReportAsync(string stage, double? progress = null, CancellationToken cancellationToken = default)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfDbContext>();
        await db.Jobs
            .Where(x => x.Id == Id && x.State == JobState.Running)
            .ExecuteUpdateAsync(x => x.SetProperty(j => j.Stage, stage).SetProperty(j => j.Progress, progress), cancellationToken);
    }
}
