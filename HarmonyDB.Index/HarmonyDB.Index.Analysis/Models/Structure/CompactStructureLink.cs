namespace HarmonyDB.Index.Analysis.Models.Structure;

/// <summary>
/// A <see cref="StructureLink"/> with its loop and its song by their numbers in <see cref="Ids.IndexIds"/>.
/// </summary>
public readonly record struct CompactStructureLink(
    int Loop,
    int Song,
    byte NormalizationRoot,
    float Occurrences,
    float Successions,
    float Coverage);
