using System.Text.Json;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Clients.OwnChords;

/// <summary>
/// Moving the own chords an account wrote to another account in their store, when the first is merged into the second:
/// written to the outbox in the merge's transaction (<see cref="Enqueue"/>), sent by <see cref="OwnChordsAuthorMoveWorker"/>
/// until the store has done it.
/// </summary>
public static class OwnChordsAuthorMove
{
    public const string OutboxKind = "own_chords.move_author";

    public static void Enqueue(ShelfDbContext db, Guid fromAccountId, Guid toAccountId, DateTimeOffset now)
        => db.Outbox.Add(new()
        {
            Kind = OutboxKind,
            Payload = JsonSerializer.Serialize(new Payload(fromAccountId, toAccountId)),
            CreatedAt = now,
            NextAttemptAt = now,
        });

    internal sealed record Payload(Guid From, Guid To);
}

/// <summary>
/// Sends the outbox's moves of own chords, oldest first, each until it succeeds (with a growing pause after a failure).
/// A message is taken under a row lock that other copies of the app skip. Idle while the store is not configured.
/// </summary>
internal sealed class OwnChordsAuthorMoveWorker(
    IServiceScopeFactory scopes,
    IOptionsMonitor<OwnChordsClientOptions> options,
    TimeProvider time,
    ILogger<OwnChordsAuthorMoveWorker> logger) : BackgroundService
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
                    logger.LogError(exception, "The moves of own chords failed to run.");
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
        var client = scope.ServiceProvider.GetRequiredService<OwnChordsClient>();
        var now = time.GetUtcNow();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var message = await db.Outbox
            .FromSql($"""
                      SELECT * FROM outbox
                      WHERE kind = {OwnChordsAuthorMove.OutboxKind} AND processed_at IS NULL AND next_attempt_at <= {now}
                      ORDER BY id
                      LIMIT 1
                      FOR UPDATE SKIP LOCKED
                      """)
            .SingleOrDefaultAsync(cancellationToken);
        if (message == null)
            return false;

        var payload = JsonSerializer.Deserialize<OwnChordsAuthorMove.Payload>(message.Payload)!;
        message.Attempts++;
        try
        {
            await client.MoveAuthorAsync(payload.From, payload.To, cancellationToken);
            message.ProcessedAt = now;
            message.LastError = null;
        }
        catch (Foundation.Errors.ShelfException exception)
        {
            message.LastError = exception.Code;
            message.NextAttemptAt = now + TimeSpan.FromSeconds(Math.Min(3600, 10 * Math.Pow(2, Math.Min(message.Attempts, 12))));
            logger.LogWarning("Moving the own chords of {From} to {To} failed: {Code}.", payload.From, payload.To, exception.Code);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
