using System.Text.Json;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Analyses.Services;

/// <summary>
/// Forgetting the files of analyses in the music service's store: written to the outbox in the transaction that drops
/// them (<see cref="Enqueue"/>), sent by <see cref="AnalysisBlobDeletionWorker"/> until the music service has done it.
/// </summary>
public static class AnalysisBlobDeletions
{
    public const string OutboxKind = "analyses.delete_blob";

    public static void Enqueue(ShelfDbContext db, string blobKey, DateTimeOffset now)
        => db.Outbox.Add(new()
        {
            Kind = OutboxKind,
            Payload = JsonSerializer.Serialize(new Payload(blobKey)),
            CreatedAt = now,
            NextAttemptAt = now,
        });

    internal sealed record Payload(string BlobKey);
}

/// <summary>
/// Sends the outbox's deletions of analyses' files, oldest first, each until it succeeds (with a growing pause after a
/// failure). A file that an analysis names again meanwhile (its content, or a file put since) is kept. Several copies of
/// the app may run it at once: a message is taken under a row lock that the others skip. Idle while the music service
/// is not configured.
/// </summary>
internal sealed class AnalysisBlobDeletionWorker(
    IServiceScopeFactory scopes,
    IOptionsMonitor<AudioClientOptions> options,
    TimeProvider time,
    ILogger<AnalysisBlobDeletionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Poll = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.CurrentValue.IsConfigured)
            {
                try
                {
                    while (await SendOneAsync(stoppingToken))
                    {
                    }
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(exception, "The deletions of analyses' files failed to run.");
                }
            }

            try
            {
                await Task.Delay(Poll, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task<bool> SendOneAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfDbContext>();
        var blobs = scope.ServiceProvider.GetRequiredService<AudioBlobsClient>();
        var now = time.GetUtcNow();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var message = await db.Outbox
            .FromSql($"""
                      SELECT * FROM outbox
                      WHERE kind = {AnalysisBlobDeletions.OutboxKind} AND processed_at IS NULL AND next_attempt_at <= {now}
                      ORDER BY id
                      LIMIT 1
                      FOR UPDATE SKIP LOCKED
                      """)
            .SingleOrDefaultAsync(cancellationToken);
        if (message == null)
            return false;

        var key = JsonSerializer.Deserialize<AnalysisBlobDeletions.Payload>(message.Payload)!.BlobKey;
        message.Attempts++;

        // the key is held until this transaction ends: an upload of the same file waits for the deletion, or was done
        // before it and is seen here
        await AnalysisStore.LockBlobAsync(db, key, cancellationToken);

        // named again since it was dropped: kept
        var named = await db.AnalysisFiles.AnyAsync(x => x.BlobKey == key, cancellationToken)
                    || await db.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM analysis_contents WHERE position({key} in content::text) > 0) AS \"Value\"")
                        .SingleAsync(cancellationToken);
        try
        {
            if (!named)
                await blobs.DeleteAsync(key, cancellationToken);

            message.ProcessedAt = now;
            message.LastError = null;
        }
        catch (AudioException exception)
        {
            message.LastError = exception.Code;
            message.NextAttemptAt = now + TimeSpan.FromSeconds(Math.Min(3600, 10 * Math.Pow(2, Math.Min(message.Attempts, 12))));
            logger.LogWarning("Deleting the analyses' file {Key} failed: {Code}.", key, exception.Code);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
