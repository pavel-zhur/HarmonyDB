namespace HarmonyDB.Index.Analysis.Models.Ids;

/// <summary>
/// The ids the index's caches share: the songs' external ids and the loops' normalized progressions.
/// One instance serves all the caches, so that each id string is kept once and the caches meet by numbers.
/// </summary>
public class IndexIds
{
    public IdTable Songs { get; } = new();

    public IdTable Loops { get; } = new();
}
