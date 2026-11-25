using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HarmonyDB.Shelf.Database.Model;

public enum JobState
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
}

/// <summary>
/// Background work a person started (illustrations, a playlist's sync, a PDF book …), with its state, so leaving the
/// page loses nothing. A worker takes a queued job by locking it (<see cref="LockedUntil"/>).
/// </summary>
public class Job
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    /// <summary>
    /// What work it is, a stable code ("illustrations", "playlist_sync", "book").
    /// </summary>
    public required string Kind { get; set; }

    /// <summary>
    /// What it works on (chords' id, a playlist's id …), to find the work going on for a thing.
    /// </summary>
    public string? SubjectId { get; set; }

    public JobState State { get; set; }

    /// <summary>
    /// 0 … 1, when known.
    /// </summary>
    public double? Progress { get; set; }

    /// <summary>
    /// The step it is at, a code the frontend words.
    /// </summary>
    public string? Stage { get; set; }

    /// <summary>
    /// What it was asked to do, JSON.
    /// </summary>
    public required string Input { get; set; }

    /// <summary>
    /// What came of it, JSON, when it succeeded.
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Why it failed, a code the frontend words.
    /// </summary>
    public string? ErrorCode { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public JobOutput? Output { get; set; }
}

/// <summary>
/// The file a job made (a PDF), kept until it is fetched or expires.
/// </summary>
public class JobOutput
{
    public Guid JobId { get; set; }

    public Job Job { get; set; } = null!;

    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>
/// A call to another service that must happen after a change is committed (purging what the music service cached under
/// a removed music link …): written in the same transaction as the change, sent by a worker until it succeeds.
/// </summary>
public class OutboxMessage
{
    public long Id { get; set; }

    /// <summary>
    /// What is to be done, a stable code.
    /// </summary>
    public required string Kind { get; set; }

    /// <summary>
    /// JSON.
    /// </summary>
    public required string Payload { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}

/// <summary>
/// A record of a change that matters later (accounts merged, permissions given or taken, a profile deleted). Not tied to
/// the account by a key, so it outlives it.
/// </summary>
public class AuditEntry
{
    public long Id { get; set; }

    public DateTimeOffset At { get; set; }

    /// <summary>
    /// Whose data it is about.
    /// </summary>
    public Guid? AccountId { get; set; }

    /// <summary>
    /// Who did it: "account:&lt;id&gt;", "internal:&lt;client name&gt;", "tool:&lt;name&gt;".
    /// </summary>
    public required string Actor { get; set; }

    /// <summary>
    /// What was done, a stable code ("accounts_merged", "permission_granted" …).
    /// </summary>
    public required string Action { get; set; }

    /// <summary>
    /// The details, JSON.
    /// </summary>
    public string? Data { get; set; }
}

internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs");
        builder.Property(x => x.Input).HasColumnType("jsonb");
        builder.Property(x => x.Result).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.AccountId, x.CreatedAt });
        builder.HasIndex(x => new { x.Kind, x.SubjectId });
        builder.HasIndex(x => new { x.State, x.CreatedAt }).HasFilter("state IN ('queued', 'running')");
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Output).WithOne(x => x.Job).HasForeignKey<JobOutput>(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class JobOutputConfiguration : IEntityTypeConfiguration<JobOutput>
{
    public void Configure(EntityTypeBuilder<JobOutput> builder)
    {
        builder.ToTable("job_outputs");
        builder.HasKey(x => x.JobId);
        builder.HasIndex(x => x.ExpiresAt);
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox");
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Payload).HasColumnType("jsonb");
        builder.HasIndex(x => x.NextAttemptAt).HasFilter("processed_at IS NULL");
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_log");
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Data).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.AccountId, x.At });
    }
}
