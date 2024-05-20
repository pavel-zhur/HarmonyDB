namespace OneShelf.Frontend.Api.Model;

public record FrontendOptions
{
    public Uri? RegenerationUri { get; init; }
    
    public string? RegenerationKey { get; init; }

    public Uri? TelegramPingUri { get; init; }
}