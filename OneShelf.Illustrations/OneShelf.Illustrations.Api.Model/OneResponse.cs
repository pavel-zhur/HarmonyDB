namespace OneShelf.Illustrations.Api.Model;

public class OneResponse
{
    public required Dictionary<int, List<List<string>>> Prompts { get; init; }

    [Obsolete]
    public required Dictionary<int, List<List<List<Guid>>>> ImageIds { get; init; }

    public required Dictionary<int, List<List<List<ImagePublicUrl>>>> ImagePublicUrls { get; init; }

    public required Dictionary<int, string> CustomSystemMessages { get; init; }

    public required string LyricsTrace { get; init; }

    public required DateTime EarliestCreatedOn { get; init; }
    
    public required DateTime LatestCreatedOn { get; init; }
}