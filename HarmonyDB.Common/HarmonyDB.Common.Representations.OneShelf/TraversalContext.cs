namespace HarmonyDB.Common.Representations.OneShelf;

internal class TraversalContext
{
    public bool IsInsideChord { get; set; }

    public List<NodeChord>? SiblingChords { get; set; }

    public int ChordIndex { get; set; }

    /// <summary>
    /// The sheet's bars, when it has them: the places of its chords and marks are in them.
    /// </summary>
    public SheetTimes? Times { get; init; }
}