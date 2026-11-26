using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HarmonyDB.Shelf.Database.Model;

/// <summary>
/// A link pasted into an import of chords by links that the sources did not find (no source reads such an address, or
/// what is there is not open to the person): one row for each import it came in, for the admin site. The link is not
/// the person's data: when they delete their profile, the row stays and only who pasted it goes.
/// </summary>
public class UnreadLink
{
    public long Id { get; set; }

    /// <summary>
    /// The link as it was pasted.
    /// </summary>
    public required string Link { get; set; }

    /// <summary>
    /// The import it came in (a job's id; the job itself goes in its time): an import run again counts it once.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Who pasted it; null once their profile is deleted.
    /// </summary>
    public Guid? AccountId { get; set; }

    public Account? Account { get; set; }

    public DateTimeOffset SeenAt { get; set; }
}

internal sealed class UnreadLinkConfiguration : IEntityTypeConfiguration<UnreadLink>
{
    public void Configure(EntityTypeBuilder<UnreadLink> builder)
    {
        builder.ToTable("unread_links");
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.HasIndex(x => new { x.JobId, x.Link }).IsUnique();
        builder.HasIndex(x => x.Link);
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.SetNull);
    }
}
