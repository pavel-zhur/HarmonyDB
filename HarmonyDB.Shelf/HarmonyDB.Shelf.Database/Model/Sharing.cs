using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HarmonyDB.Shelf.Database.Model;

public enum ShareTargetKind
{
    /// <summary>A playlist: it opens to the holder only as its access allows.</summary>
    Playlist,

    /// <summary>A song of a playlist, optionally on one of its chords variants: it opens without sign-in; from a
    /// private playlist it shows the song and its chords, not the playlist.</summary>
    Song,

    /// <summary>Chords alone: they open without sign-in.</summary>
    Chords,
}

/// <summary>
/// A link (and its QR code) to a playlist, a song of a playlist, or chords. One link per target: sharing it again gives
/// the same link. A link of chords may instead carry its sharer's access (<see cref="CarriedKeys"/>): one such link per
/// sharer and chords, removed by its sharer.
/// </summary>
public class ShareLink
{
    /// <summary>
    /// The random, URL-safe token of the link.
    /// </summary>
    public required string Token { get; set; }

    public ShareTargetKind Kind { get; set; }

    public Guid? PlaylistId { get; set; }

    public Playlist? Playlist { get; set; }

    public Guid? SongId { get; set; }

    public PlaylistSong? Song { get; set; }

    public Guid? ChordsId { get; set; }

    public ChordsItem? Chords { get; set; }

    public Guid? CreatedById { get; set; }

    public Account? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// For a link of chords that carries its sharer's access: the sharer's custom keys when the link was made (the
    /// chords are read by them, never by the account). Null for an ordinary link.
    /// </summary>
    public List<string>? CarriedKeys { get; set; }
}

internal sealed class ShareLinkConfiguration : IEntityTypeConfiguration<ShareLink>
{
    public void Configure(EntityTypeBuilder<ShareLink> builder)
    {
        builder.ToTable("share_links", x =>
        {
            x.HasCheckConstraint(
                "ck_share_links_target",
                """
                (kind = 'playlist' AND playlist_id IS NOT NULL AND song_id IS NULL AND chords_id IS NULL)
                OR (kind = 'song' AND song_id IS NOT NULL AND playlist_id IS NULL)
                OR (kind = 'chords' AND chords_id IS NOT NULL AND playlist_id IS NULL AND song_id IS NULL)
                """);
            x.HasCheckConstraint("ck_share_links_carried_keys", "carried_keys IS NULL OR kind = 'chords'");
        });
        builder.HasKey(x => x.Token);
        builder.HasIndex(x => new { x.Kind, x.PlaylistId, x.SongId, x.ChordsId }).IsUnique().AreNullsDistinct(false).HasFilter("carried_keys IS NULL");
        builder.HasIndex(x => new { x.ChordsId, x.CreatedById }).IsUnique().HasFilter("carried_keys IS NOT NULL");
        builder.HasIndex(x => x.PlaylistId);
        builder.HasIndex(x => x.SongId);
        builder.HasIndex(x => x.ChordsId);
        builder.HasOne(x => x.Playlist).WithMany().HasForeignKey(x => x.PlaylistId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Song).WithMany().HasForeignKey(x => x.SongId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Chords).WithMany().HasForeignKey(x => x.ChordsId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
    }
}
