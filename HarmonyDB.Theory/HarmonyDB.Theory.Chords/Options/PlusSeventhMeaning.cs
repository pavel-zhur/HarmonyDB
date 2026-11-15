namespace HarmonyDB.Theory.Chords.Options;

/// <summary>
/// What a plus next to the seventh means: "C7+", "C+7", "Cm7+", "Cm+7".
/// </summary>
public enum PlusSeventhMeaning
{
    /// <summary>
    /// The plus raises the seventh: "C7+" and "C+7" are Cmaj7, "Cm7+" is Cm(maj7).
    /// </summary>
    MajorSeventh,

    /// <summary>
    /// The plus is the augmented fifth: "C7+" and "C+7" are C7(#5).
    /// </summary>
    AugmentedFifth,

    /// <summary>
    /// Decided for each song: the augmented fifth when the song spells the major seventh in another way ("Cmaj7", "CM7", "C7M"),
    /// or when a chord with the plus follows the plain seventh chord on the same root ("A7 A7+", the rising fifth);
    /// the major seventh otherwise. Parsing needs the song's chords (<see cref="Parsers.ChordParser.ParseSong"/>).
    /// </summary>
    Detect,
}
