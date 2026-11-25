namespace HarmonyDB.Index.Api.Models;

/// <summary>
/// The scopes of the index's internal clients (configuration <c>Index:InternalClients</c>).
/// </summary>
public static class IndexInternalScopes
{
    /// <summary>
    /// The relay of the source contract, version 2, and the analysis tools.
    /// </summary>
    public const string Index = "index";
}
