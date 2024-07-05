namespace HarmonyDB.Index.BusinessLogic.Models;

public record struct SongLoopLink
{
    public required byte NormalizationRoot { get; init; }
    
    public required short Occurrences { get; init; }
    
    public required short Successions { get; init; }
}