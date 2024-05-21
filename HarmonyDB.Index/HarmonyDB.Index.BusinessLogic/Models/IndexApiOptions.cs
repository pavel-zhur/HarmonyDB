namespace HarmonyDB.Index.BusinessLogic.Models;

public class IndexApiOptions
{
    public required IReadOnlyDictionary<string, string> SourcesExternalIdPrefixes { get; init; }
}