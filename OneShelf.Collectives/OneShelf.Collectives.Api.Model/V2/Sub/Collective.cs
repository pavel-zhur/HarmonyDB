namespace OneShelf.Collectives.Api.Model.V2.Sub;

public class Collective
{
    public required List<string> Authors { get; set; }

    public required string Title { get; set; }

    public required string Contents { get; set; }

    public CollectiveVisibility Visibility { get; set; }

    public string? Description { get; set; }

    public string? Tonality { get; set; }

    public int? Bpm { get; set; }

    public string? VersionComment { get; set; }
}