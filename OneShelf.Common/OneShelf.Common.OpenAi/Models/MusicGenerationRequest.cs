namespace OneShelf.Common.OpenAi.Models;

public class MusicGenerationRequest
{
    public required string Prompt { get; init; }
    public string? NegativePrompt { get; init; }
    public required long? UserId { get; init; }
    public required string UseCase { get; init; }
    public required string? AdditionalBillingInfo { get; init; }
    public required int? DomainId { get; init; }
    public required long? ChatId { get; init; }
    public required string Model { get; init; } // e.g., "lyria-002"
}