namespace OneShelf.Videos.App.Models;

public record VideosOptions
{
    public required string BasePath { get; init; }
    
    [Obsolete]
    public string? AuthorizationScheme { get; init; }

    [Obsolete]
    public string? AuthorizationParameter { get; init; }
}