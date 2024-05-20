using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OneShelf.Common.Database.Songs.Model;

public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long Id { get; set; }

    public int TenantId { get; set; }

    public required Tenant Tenant { get; set; }

    public string Title { get; set; } = null!;

    public bool IsAuthorizedToUseIllustrations { get; set; }

    public DateTime? AuthorizedToUseIllustrationAlterationsTemporarilySince { get; set; }

    public bool IsAuthorizedToUseIllustrationAlterationsPermanently { get; set; }

    public ICollection<Like> Likes { get; set; } = null!;

    public ICollection<Comment> Comments { get; set; } = null!;

    public ICollection<Song> SongsCreated { get; set; } = null!;

    public ICollection<Version> Versions { get; set; } = null!;

    public ICollection<Interaction> Interactions { get; set; } = null!;

    public ICollection<VisitedSearch> HistorySearches { get; set; } = null!;

    public ICollection<VisitedChords> HistoryChordsViews { get; set; } = null!;
    
    public ICollection<LikeCategory> LikeCategories { get; set; } = null!;

    public ICollection<BillingUsage> BillingUsages { get; set; } = null!;

    public required DateTime CreatedOn { get; init; }
}