namespace OneShelf.Frontend.Api.Model.V3.Illustrations;

public class SongIllustrations
{
    public required IReadOnlyList<SongIllustration> Illustrations { get; init; }
    public required DateTime EarliestCreatedOn { get; init; }
    public required DateTime LatestCreatedOn { get; init; }
}