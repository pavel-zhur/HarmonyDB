namespace HarmonyDB.Index.Api.Model.VExternal1;

public record StructureSongsResponse : PagedResponseBase
{
    public required List<StructureSongTonality> Songs { get; init; }
    
    public required StructureSongsResponseDistributions Distributions { get; init; }
}