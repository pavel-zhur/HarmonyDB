namespace OneShelf.Videos.App.Models;

public record VideosOptions
{
    public required string BasePath { get; init; }
    
    public string? LocalCachePath { get; init; }

    public string? AuthorizationScheme { get; init; }

    public string? AuthorizationParameter { get; init; }
}