namespace OneShelf.Authorization.Api.Model;

public record AuthorizationOptions
{
    public bool AllowBadHash { get; init; }

    public required IReadOnlyList<string> BotTokens { get; init; }

    public string? BadHash { get; init; }
}