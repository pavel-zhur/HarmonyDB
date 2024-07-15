namespace HarmonyDB.Index.Analysis.Models.Index;

public record StructureLink(
    string Normalized,
    string ExternalId,
    byte NormalizationRoot,
    float Occurrences,
    float Successions,
    float Coverage)
{
    /// <summary>
    /// Corresponds to the key (it is different for the same normalized progression if and only if it is in different keys).
    /// More precisely, it is the root of the first movement of the normalized sequence, wherever its corresponding original sequence movement is.
    /// </summary>
    public byte NormalizationRoot { get; } = NormalizationRoot;
}