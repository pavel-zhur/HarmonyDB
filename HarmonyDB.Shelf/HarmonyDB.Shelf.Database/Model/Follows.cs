using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HarmonyDB.Shelf.Database.Model;

/// <summary>
/// A playlist a person keeps without being its member: one they may look at (<see cref="PlaylistAccess.Public"/>, or
/// <see cref="PlaylistAccess.Link"/> by its link). A follower adds nothing, is not among the members, and their marks
/// and the members' are not seen by each other. While the playlist is private, its follows count for nothing.
/// </summary>
public class PlaylistFollow : IVersioned, ITombstoned
{
    public Guid Id { get; set; }

    public Guid PlaylistId { get; set; }

    public Playlist Playlist { get; set; } = null!;

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    /// <summary>
    /// Whether the playlist's songs come into the follower's library.
    /// </summary>
    public bool ShowInLibrary { get; set; } = true;

    public DateTimeOffset FollowedAt { get; set; }

    public long Version { get; set; }

    /// <summary>
    /// When the person stopped following it.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }
}

internal sealed class PlaylistFollowConfiguration : IEntityTypeConfiguration<PlaylistFollow>
{
    public void Configure(EntityTypeBuilder<PlaylistFollow> builder)
    {
        builder.ToTable("playlist_follows");
        builder.HasIndex(x => new { x.PlaylistId, x.AccountId }).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(x => x.AccountId);
        builder.HasIndex(x => x.Version);
        builder.HasOne(x => x.Playlist).WithMany().HasForeignKey(x => x.PlaylistId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
