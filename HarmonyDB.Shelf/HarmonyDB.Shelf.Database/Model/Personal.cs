using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HarmonyDB.Shelf.Database.Model;

/// <summary>
/// A person's mark on chords (hearts of several colours and other signs; the kinds are the app's vocabulary).
/// A person's own; others see it only in playlists where both are members.
/// </summary>
public class Mark : IVersioned, ITombstoned
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public Guid ChordsId { get; set; }

    public ChordsItem Chords { get; set; } = null!;

    /// <summary>
    /// The mark's kind: one of the keys of <c>MarkKinds</c> (HarmonyDB.Shelf.Model).
    /// </summary>
    public required string Kind { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public long Version { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// A person's key (transposition) for chords: without a playlist, "mine" — everywhere; with a playlist, "here" — in that
/// playlist only, over "mine". Both are kept even when equal. Others' "here" is seen by the playlist's members.
/// </summary>
public class Transposition : IVersioned, ITombstoned
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public Guid ChordsId { get; set; }

    public ChordsItem Chords { get; set; } = null!;

    public Guid? PlaylistId { get; set; }

    public Playlist? Playlist { get; set; }

    /// <summary>
    /// Semitones, −11 … +11.
    /// </summary>
    public int Semitones { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// A person's text note: personal, on chords (only they see it), or on a song of a playlist (its members read it).
/// Exactly one of <see cref="ChordsId"/> and <see cref="SongId"/> is set; one note a person per target.
/// </summary>
public class Note : IVersioned, ITombstoned
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public Guid? ChordsId { get; set; }

    public ChordsItem? Chords { get; set; }

    public Guid? SongId { get; set; }

    public PlaylistSong? Song { get; set; }

    public required string Text { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// What a person opened, and when: chords, a song of a playlist, or chords as a variant of a song.
/// </summary>
public class RecentItem : IVersioned, ITombstoned
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public Guid? ChordsId { get; set; }

    public ChordsItem? Chords { get; set; }

    public Guid? SongId { get; set; }

    public PlaylistSong? Song { get; set; }

    public DateTimeOffset OpenedAt { get; set; }

    public long Version { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}

internal sealed class MarkConfiguration : IEntityTypeConfiguration<Mark>
{
    public void Configure(EntityTypeBuilder<Mark> builder)
    {
        builder.ToTable("marks");
        builder.HasIndex(x => new { x.AccountId, x.ChordsId, x.Kind }).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(x => x.ChordsId);
        builder.HasIndex(x => x.Version);
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Chords).WithMany().HasForeignKey(x => x.ChordsId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TranspositionConfiguration : IEntityTypeConfiguration<Transposition>
{
    public void Configure(EntityTypeBuilder<Transposition> builder)
    {
        builder.ToTable("transpositions", x => x.HasCheckConstraint(
            "ck_transpositions_semitones",
            "semitones BETWEEN -11 AND 11"));
        builder.HasIndex(x => new { x.AccountId, x.ChordsId, x.PlaylistId }).IsUnique().AreNullsDistinct(false).HasFilter("deleted_at IS NULL");
        builder.HasIndex(x => new { x.PlaylistId, x.ChordsId });
        builder.HasIndex(x => x.ChordsId);
        builder.HasIndex(x => x.Version);
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Chords).WithMany().HasForeignKey(x => x.ChordsId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Playlist).WithMany().HasForeignKey(x => x.PlaylistId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("notes", x => x.HasCheckConstraint(
            "ck_notes_one_target",
            "num_nonnulls(chords_id, song_id) = 1"));
        builder.HasIndex(x => new { x.AccountId, x.ChordsId }).IsUnique().HasFilter("chords_id IS NOT NULL AND deleted_at IS NULL");
        builder.HasIndex(x => new { x.AccountId, x.SongId }).IsUnique().HasFilter("song_id IS NOT NULL AND deleted_at IS NULL");
        builder.HasIndex(x => x.SongId);
        builder.HasIndex(x => x.Version);
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Chords).WithMany().HasForeignKey(x => x.ChordsId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Song).WithMany().HasForeignKey(x => x.SongId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RecentItemConfiguration : IEntityTypeConfiguration<RecentItem>
{
    public void Configure(EntityTypeBuilder<RecentItem> builder)
    {
        builder.ToTable("recent", x => x.HasCheckConstraint(
            "ck_recent_target",
            "num_nonnulls(chords_id, song_id) >= 1"));
        builder.HasIndex(x => new { x.AccountId, x.ChordsId, x.SongId }).IsUnique().AreNullsDistinct(false).HasFilter("deleted_at IS NULL");
        builder.HasIndex(x => new { x.AccountId, x.OpenedAt });
        builder.HasIndex(x => x.ChordsId);
        builder.HasIndex(x => x.SongId);
        builder.HasIndex(x => x.Version);
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Chords).WithMany().HasForeignKey(x => x.ChordsId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Song).WithMany().HasForeignKey(x => x.SongId).OnDelete(DeleteBehavior.Cascade);
    }
}
