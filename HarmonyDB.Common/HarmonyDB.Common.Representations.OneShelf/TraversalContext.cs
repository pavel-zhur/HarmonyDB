namespace HarmonyDB.Common.Representations.OneShelf;

internal class TraversalContext
{
    public bool IsInsideChord { get; set; }

    public List<NodeChord>? SiblingChords { get; set; }

    public int ChordIndex { get; set; }
}