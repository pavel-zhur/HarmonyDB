namespace OneShelf.Illustrations.Api.Model;

public class GenerateRequest
{
    public required string Url { get; init; }

    public List<GenerationIndex>? GenerateIndices { get; init; }
        
    public int? SpecialSystemMessage { get; init; }

    public string? CustomSystemMessage { get; init; }

    public string? AlterationKey { get; init; }
    
    public long? UserId { get; init; }

    public string? AdditionalBillingInfo { get; init; }
}