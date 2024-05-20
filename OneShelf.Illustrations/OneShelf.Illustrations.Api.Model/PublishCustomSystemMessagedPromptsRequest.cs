namespace OneShelf.Illustrations.Api.Model;

public class PublishCustomSystemMessagedPromptsRequest
{
    public required Guid IllustrationId { get; init; }
    
    public required int SpecialSystemMessage { get; init; }
    
    public string? AlterationKey { get; set; }
}