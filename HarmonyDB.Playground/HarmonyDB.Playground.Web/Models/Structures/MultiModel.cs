namespace HarmonyDB.Playground.Web.Models.Structures;

public record MultiModel
{
    public List<string> ExternalIds { get; set; } = new();
    
    public int? ToRemove { get; set; }

    public bool IncludeTrace { get; init; }
}