using System.Text.Json;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Clients.Audio;

/// <summary>
/// Purging what the music service keeps under a removed music link: written to the outbox in the transaction that
/// removes the link (<see cref="Enqueue"/>), sent by <see cref="AudioSubjectPurgeWorker"/> until the music service has
/// done it.
/// </summary>
public static class AudioSubjectPurge
{
    public const string OutboxKind = "audio.purge_subject";

    /// <summary>
    /// Adds the purge of a subject (a music link's id) to the context's changes.
    /// </summary>
    public static void Enqueue(ShelfDbContext db, Guid musicLinkId, DateTimeOffset now)
        => db.Outbox.Add(new()
        {
            Kind = OutboxKind,
            Payload = JsonSerializer.Serialize(new Payload(musicLinkId.ToString())),
            CreatedAt = now,
            NextAttemptAt = now,
        });

    internal sealed record Payload(string Subject);
}

public static class AudioClientExtensions
{
    /// <summary>
    /// The music service's client, and the worker that sends the purges of its subjects. Safe to call from every area
    /// that needs it.
    /// </summary>
    public static IServiceCollection AddAudioClient(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(x => x.ServiceType == typeof(AudioClient)))
            return services;

        services.AddOptions<AudioClientOptions>().Bind(configuration.GetSection(AudioClientOptions.SectionName)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AudioClientOptions>, AudioClientOptionsValidator>());
        // each call has its own timeout (AudioClient.DefaultTimeout, or longer where it says)
        services.AddHttpClient<AudioClient>(x => x.Timeout = Timeout.InfiniteTimeSpan);
        services.AddHostedService<AudioSubjectPurgeWorker>();
        return services;
    }
}

/// <summary>
/// Sends the outbox's purges of music service subjects, oldest first, each until it succeeds (with a growing pause
/// after a failure). Several copies of the app may run it at once: a message is taken under a row lock that the others
/// skip. Idle while the music service is not configured.
/// </summary>
internal sealed class AudioSubjectPurgeWorker(
    IServiceScopeFactory scopes,
    IOptionsMonitor<AudioClientOptions> options,
    TimeProvider time,
    ILogger<AudioSubjectPurgeWorker> logger) : BackgroundService
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
                    logger.LogError(exception, "The purges of the music service's subjects failed to run.");
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

    /// <summary>
    /// Sends one due message, if there is one.
    /// </summary>
    private async Task<bool> SendOneAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfDbContext>();
        var client = scope.ServiceProvider.GetRequiredService<AudioClient>();
        var now = time.GetUtcNow();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var message = await db.Outbox
            .FromSql($"""
                      SELECT * FROM outbox
                      WHERE kind = {AudioSubjectPurge.OutboxKind} AND processed_at IS NULL AND next_attempt_at <= {now}
                      ORDER BY id
                      LIMIT 1
                      FOR UPDATE SKIP LOCKED
                      """)
            .SingleOrDefaultAsync(cancellationToken);
        if (message == null)
            return false;

        var payload = JsonSerializer.Deserialize<AudioSubjectPurge.Payload>(message.Payload)!;
        message.Attempts++;
        try
        {
            await client.PurgeSubjectAsync(payload.Subject, cancellationToken);
            message.ProcessedAt = now;
            message.LastError = null;
        }
        catch (AudioException exception)
        {
            message.LastError = exception.Code;
            message.NextAttemptAt = now + Backoff(message.Attempts);
            logger.LogWarning("Purging the music service's subject {Subject} failed: {Code}.", payload.Subject, exception.Code);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static TimeSpan Backoff(int attempts) => TimeSpan.FromSeconds(Math.Min(3600, 10 * Math.Pow(2, Math.Min(attempts, 12))));
}
