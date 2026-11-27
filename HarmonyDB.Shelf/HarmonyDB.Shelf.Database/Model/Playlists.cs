using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HarmonyDB.Shelf.Database.Model;

public enum PlaylistAccess
{
    /// <summary>Its members only.</summary>
    Private,

    /// <summary>Anyone holding its link may look.</summary>
    Link,

    /// <summary>Open to all, and listed.</summary>
    Public,
}

public enum PlaylistAddPolicy
{
    AllMembers,
    Admins,
}

/// <summary>
/// The playlists the app keeps for each person, which cannot be deleted (their songs can).
/// </summary>
public enum PlaylistSystemKind
{
    None,
    ImportedChords,
    OwnChords,
    OwnAnalyses,
}

public class Playlist : IVersioned, ITombstoned
{
    public Guid Id { get; set; }

    /// <summary>
    /// The owner. Their member row has the role <see cref="PlaylistRole.Owner"/>; the two are changed together.
    /// When the owner deletes their profile, a shared playlist passes to its longest-standing admin, else member.
    /// </summary>
    public Guid OwnerId { get; set; }

    public Account Owner { get; set; } = null!;

    public required string Title { get; set; }

    public string? Description { get; set; }

    public PlaylistAccess Access { get; set; }

    public PlaylistAddPolicy AddPolicy { get; set; }

    /// <summary>
    /// The music service where recordings for its songs are looked for first; the other is the fallback.
    /// Null: not chosen.
    /// </summary>
    public MusicService? SearchFirst { get; set; }

    public PlaylistSystemKind SystemKind { get; set; }

    /// <summary>
    /// The music service's playlist it was imported from, if it was.
    /// </summary>
    public MusicService? OriginService { get; set; }

    public string? OriginPlaylistId { get; set; }

    public string? OriginUrl { get; set; }

    /// <summary>
    /// Who imported it: only that account sees and may press the sync, whoever owns the playlist later.
    /// </summary>
    public Guid? ImportedById { get; set; }

    public Account? ImportedBy { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public List<PlaylistMember> Members { get; set; } = [];

    public List<PlaylistSong> Songs { get; set; } = [];
}

public enum PlaylistRole
{
    Owner,
    Admin,
    Member,
}

public class PlaylistMember : IVersioned, ITombstoned
{
    public Guid Id { get; set; }

    public Guid PlaylistId { get; set; }

    public Playlist Playlist { get; set; } = null!;

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public PlaylistRole Role { get; set; }

    /// <summary>
    /// Whether this playlist's songs come into the member's library.
    /// </summary>
    public bool ShowInLibrary { get; set; } = true;

    public DateTimeOffset JoinedAt { get; set; }

    public long Version { get; set; }

    /// <summary>
    /// When the member left (or was removed).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// A song exists only inside a playlist: its title and artists are chosen for this playlist; it groups chords and
/// recordings (its items).
/// </summary>
public class PlaylistSong : IVersioned, ITombstoned
{
    public Guid Id { get; set; }

    public Guid PlaylistId { get; set; }

    public Playlist Playlist { get; set; } = null!;

    public required string Title { get; set; }

    public List<string> Artists { get; set; } = [];

    /// <summary>
    /// The order in the playlist: songs are sorted by it (then by id). A song moved between two gets a value between
    /// theirs.
    /// </summary>
    public double Position { get; set; }

    public Guid? AddedById { get; set; }

    public Account? AddedBy { get; set; }

    public DateTimeOffset AddedAt { get; set; }

    /// <summary>
    /// The track of the origin playlist (see <see cref="Playlist.OriginService"/>) this song was imported from.
    /// </summary>
    public string? OriginTrackId { get; set; }

    public long Version { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public List<PlaylistSongItem> Items { get; set; } = [];
}

/// <summary>
/// The one rule of who sees a playlist's entries.
/// </summary>
public static class PlaylistSongItemsSeen
{
    /// <summary>
    /// The entries the person sees: every entry but the private own chords someone else placed — the old shelf showed an
    /// author's private own chords to whoever added them alone. Every reading of entries for a person goes through it.
    /// </summary>
    public static IQueryable<PlaylistSongItem> SeenBy(this IQueryable<PlaylistSongItem> items, Guid? viewer)
        => items.Where(x => !x.Private || (viewer != null && x.AddedById == viewer));

    /// <summary>
    /// The songs the person sees: those with an entry they see (a song whose every entry is someone else's private own
    /// chords is not there for them, as the old shelf left such a song out).
    /// </summary>
    public static IQueryable<PlaylistSong> SeenBy(this IQueryable<PlaylistSong> songs, Guid? viewer)
        => songs.Where(s => s.Items.Any(x => x.DeletedAt == null && (!x.Private || (viewer != null && x.AddedById == viewer))));
}

/// <summary>
/// A variant of a song: chords, or a recording. Exactly one of the two is set.
/// The chords variants are lettered A, B, C … by their order among the song's chords variants that are not removed
/// (<see cref="Position"/>, then id) that the person sees (<see cref="PlaylistSongItemsSeen.SeenBy"/>), and shift when
/// one is removed.
/// </summary>
public class PlaylistSongItem : IVersioned, ITombstoned
{
    public Guid Id { get; set; }

    public Guid SongId { get; set; }

    public PlaylistSong Song { get; set; } = null!;

    public Guid? ChordsId { get; set; }

    public ChordsItem? Chords { get; set; }

    public Guid? RecordingId { get; set; }

    public Recording? Recording { get; set; }

    /// <summary>
    /// The address the chords were added by (the link a person brought, or the address of the header they were given),
    /// as the old shelf's version kept its address: passed on with every reading of them. Own chords' entries get their
    /// new address when their author edits them, so a device takes them as new.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Private own chords placed by their author: seen only by whoever added the entry (<see cref="AddedById"/>), as the
    /// old shelf showed an author's private own chords to them alone. Read through <c>SeenBy</c>.
    /// </summary>
    public bool Private { get; set; }

    public double Position { get; set; }

    /// <summary>
    /// Who added it; null when it was added automatically (a recording found for the chords) or its author is gone.
    /// </summary>
    public Guid? AddedById { get; set; }

    public Account? AddedBy { get; set; }

    public DateTimeOffset AddedAt { get; set; }

    public long Version { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// A person's choices on a song of a playlist: their main variant, and the recording they listen to.
/// </summary>
public class PlaylistSongPreference : IVersioned
{
    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public Guid SongId { get; set; }

    public PlaylistSong Song { get; set; } = null!;

    public Guid? MainItemId { get; set; }

    public PlaylistSongItem? MainItem { get; set; }

    public Guid? RecordingItemId { get; set; }

    public PlaylistSongItem? RecordingItem { get; set; }

    public long Version { get; set; }
}

/// <summary>
/// A track of the origin playlist that was removed here: the sync does not bring it back.
/// </summary>
public class PlaylistImportTombstone
{
    public Guid PlaylistId { get; set; }

    public Playlist Playlist { get; set; } = null!;

    public required string OriginTrackId { get; set; }

    public DateTimeOffset RemovedAt { get; set; }
}

internal sealed class PlaylistConfiguration : IEntityTypeConfiguration<Playlist>
{
    public void Configure(EntityTypeBuilder<Playlist> builder)
    {
        builder.ToTable("playlists", x => x.HasCheckConstraint(
            "ck_playlists_origin",
            "(origin_service IS NULL) = (origin_playlist_id IS NULL)"));
        builder.HasIndex(x => x.OwnerId);
        builder.HasIndex(x => new { x.OwnerId, x.SystemKind }).IsUnique().HasFilter("system_kind <> 'none' AND deleted_at IS NULL");
        builder.HasIndex(x => x.Access).HasFilter("access = 'public' AND deleted_at IS NULL");
        builder.HasIndex(x => x.Version);
        builder.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ImportedBy).WithMany().HasForeignKey(x => x.ImportedById).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class PlaylistMemberConfiguration : IEntityTypeConfiguration<PlaylistMember>
{
    public void Configure(EntityTypeBuilder<PlaylistMember> builder)
    {
        builder.ToTable("playlist_members");
        builder.HasIndex(x => new { x.PlaylistId, x.AccountId }).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(x => x.AccountId);
        builder.HasIndex(x => x.PlaylistId, "ix_playlist_members_one_owner").IsUnique().HasFilter("role = 'owner' AND deleted_at IS NULL");
        builder.HasIndex(x => x.Version);
        builder.HasOne(x => x.Playlist).WithMany(x => x.Members).HasForeignKey(x => x.PlaylistId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PlaylistSongConfiguration : IEntityTypeConfiguration<PlaylistSong>
{
    public void Configure(EntityTypeBuilder<PlaylistSong> builder)
    {
        builder.ToTable("playlist_songs");
        builder.HasIndex(x => new { x.PlaylistId, x.Position });
        builder.HasIndex(x => new { x.PlaylistId, x.OriginTrackId }).IsUnique().HasFilter("origin_track_id IS NOT NULL AND deleted_at IS NULL");
        builder.HasIndex(x => x.Version);
        builder.HasOne(x => x.Playlist).WithMany(x => x.Songs).HasForeignKey(x => x.PlaylistId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.AddedBy).WithMany().HasForeignKey(x => x.AddedById).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class PlaylistSongItemConfiguration : IEntityTypeConfiguration<PlaylistSongItem>
{
    public void Configure(EntityTypeBuilder<PlaylistSongItem> builder)
    {
        builder.ToTable("playlist_song_items", x => x.HasCheckConstraint(
            "ck_playlist_song_items_one_target",
            "num_nonnulls(chords_id, recording_id) = 1"));
        builder.HasIndex(x => new { x.SongId, x.ChordsId }).IsUnique().HasFilter("chords_id IS NOT NULL AND deleted_at IS NULL");
        builder.HasIndex(x => new { x.SongId, x.RecordingId }).IsUnique().HasFilter("recording_id IS NOT NULL AND deleted_at IS NULL");
        builder.HasAlternateKey(x => new { x.Id, x.SongId });
        builder.HasIndex(x => x.ChordsId);
        builder.HasIndex(x => x.RecordingId);
        builder.HasIndex(x => x.Version);
        builder.HasOne(x => x.Song).WithMany(x => x.Items).HasForeignKey(x => x.SongId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Chords).WithMany().HasForeignKey(x => x.ChordsId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Recording).WithMany().HasForeignKey(x => x.RecordingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AddedBy).WithMany().HasForeignKey(x => x.AddedById).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class PlaylistSongPreferenceConfiguration : IEntityTypeConfiguration<PlaylistSongPreference>
{
    public void Configure(EntityTypeBuilder<PlaylistSongPreference> builder)
    {
        builder.ToTable("playlist_song_prefs");
        builder.HasKey(x => new { x.AccountId, x.SongId });
        builder.HasQueryFilter(x => x.Song.DeletedAt == null);
        builder.HasIndex(x => x.SongId);
        builder.HasIndex(x => x.Version);
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Song).WithMany().HasForeignKey(x => x.SongId).OnDelete(DeleteBehavior.Cascade);
        // The chosen items are items of this very song: the keys include the song.
        builder.HasOne(x => x.MainItem).WithMany()
            .HasForeignKey(x => new { x.MainItemId, x.SongId }).HasPrincipalKey(x => new { x.Id, x.SongId })
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.RecordingItem).WithMany()
            .HasForeignKey(x => new { x.RecordingItemId, x.SongId }).HasPrincipalKey(x => new { x.Id, x.SongId })
            .OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class PlaylistImportTombstoneConfiguration : IEntityTypeConfiguration<PlaylistImportTombstone>
{
    public void Configure(EntityTypeBuilder<PlaylistImportTombstone> builder)
    {
        builder.ToTable("playlist_import_tombstones");
        builder.HasKey(x => new { x.PlaylistId, x.OriginTrackId });
        builder.HasQueryFilter(x => x.Playlist.DeletedAt == null);
        builder.HasOne(x => x.Playlist).WithMany().HasForeignKey(x => x.PlaylistId).OnDelete(DeleteBehavior.Cascade);
    }
}
