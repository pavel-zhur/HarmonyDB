namespace OneShelf.Sources.Self.Api.Models;

public class SelfApiOptions
{
    public required string ShortSourceName { get; init; }

    public required IReadOnlyList<string> Hosts { get; init; }
    
    public required string SourceName { get; init; }
}