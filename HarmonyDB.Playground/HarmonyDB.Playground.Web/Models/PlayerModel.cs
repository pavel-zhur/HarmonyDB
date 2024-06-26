namespace HarmonyDB.Playground.Web.Models;

public class PlayerModel
{
    public required IReadOnlyList<int> Bass { get; init; }
    
    public required IReadOnlyList<int> Main { get; init; }
}