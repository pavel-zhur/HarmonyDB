using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HarmonyDB.Shelf.Database.Model;

/// <summary>
/// An invitation into a playlist: the only way to join one. Made by its owner or an admin, with the role it gives,
/// an expiry and an optional limit of uses; revocable. A signed-in person opening it joins the playlist with that role.
/// (A playlist of <see cref="PlaylistAccess.Link"/> access is only looked at by its link; it is not joined by it.)
/// </summary>
public class PlaylistInvitation
{
    /// <summary>
    /// The random, URL-safe token of the invitation's link.
    /// </summary>
    public required string Token { get; set; }

    public Guid PlaylistId { get; set; }

    public Playlist Playlist { get; set; } = null!;

    /// <summary>
    /// <see cref="PlaylistRole.Member"/> or <see cref="PlaylistRole.Admin"/>.
    /// </summary>
    public PlaylistRole Role { get; set; }

    public Guid? CreatedById { get; set; }

    public Account? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// How many people may join by it; null: no limit.
    /// </summary>
    public int? MaxUses { get; set; }

    /// <summary>
    /// How many have joined by it.
    /// </summary>
    public int Uses { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}

internal sealed class PlaylistInvitationConfiguration : IEntityTypeConfiguration<PlaylistInvitation>
{
    public void Configure(EntityTypeBuilder<PlaylistInvitation> builder)
    {
        builder.ToTable("playlist_invitations", x =>
        {
            x.HasCheckConstraint("ck_playlist_invitations_role", "role IN ('member', 'admin')");
            x.HasCheckConstraint("ck_playlist_invitations_uses", "uses >= 0 AND (max_uses IS NULL OR (max_uses > 0 AND uses <= max_uses))");
        });
        builder.HasKey(x => x.Token);
        builder.HasIndex(x => x.PlaylistId);
        builder.HasQueryFilter(x => x.Playlist.DeletedAt == null);
        builder.HasOne(x => x.Playlist).WithMany().HasForeignKey(x => x.PlaylistId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
    }
}
