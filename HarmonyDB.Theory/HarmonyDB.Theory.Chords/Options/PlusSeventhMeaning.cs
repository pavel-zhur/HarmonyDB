namespace HarmonyDB.Theory.Chords.Options;

/// <summary>
/// What a plus (or a sharp) next to the seventh means: "C7+", "C+7", "Cm7+", "Cm+7".
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
}
