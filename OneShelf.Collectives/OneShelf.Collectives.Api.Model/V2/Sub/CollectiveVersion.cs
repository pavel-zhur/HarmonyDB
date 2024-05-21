namespace OneShelf.Collectives.Api.Model.V2.Sub;

public class CollectiveVersion
{
    public required Guid CollectiveId { get; set; }

    public required Collective Collective { get; set; }

    public required int VersionNumber { get; set; }

    public required DateTime CreatedOn { get; set; }

    public required int SearchTag { get; set; }

    public required long CreatedByUserId { get; set; }
    
    public required DateTime UpdatedOn { get; set; }

    public required Uri Uri { get; set; }
}