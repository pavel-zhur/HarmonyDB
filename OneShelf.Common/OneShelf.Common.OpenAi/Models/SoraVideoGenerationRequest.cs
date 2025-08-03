namespace OneShelf.Common.OpenAi.Models;

public class SoraVideoGenerationRequest
{
    public required string Prompt { get; init; }
    public required int Width { get; init; } // e.g., 1280, 720, 480
    public required int Height { get; init; } // e.g., 720, 1280, 480
    public required int Duration { get; init; } // 1-20 seconds
    public required long? UserId { get; init; }
    public required string UseCase { get; init; }
    public required string? AdditionalBillingInfo { get; init; }
    public required int? DomainId { get; init; }
    public required long? ChatId { get; init; }
    public required string Model { get; init; } // "sora"
}