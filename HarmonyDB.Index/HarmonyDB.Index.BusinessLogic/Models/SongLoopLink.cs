namespace HarmonyDB.Index.BusinessLogic.Models;

public record SongLoopLink
{
    public required string ExternalId { get; init; }
    
    public required string Normalized { get; init; }

    public required byte NormalizationRoot { get; init; }
    
    public required short Occurrences { get; init; }
    
    public required short Successions { get; init; }
}