namespace OneShelf.Common.OpenAi.Models;

public class DialogConfiguration
{
    public required string SystemMessage { get; init; }
    public required string Version { get; init; }
    public float? FrequencyPenalty { get; init; }
    public float? PresencePenalty { get; init; }
    public int? ImagesVersion { get; init; }
    public required long? UserId { get; init; }
    public required string UseCase { get; init; }
    public required string? AdditionalBillingInfo { get; init; }
    public required int? DomainId { get; init; }
    public required long? ChatId { get; init; }
}