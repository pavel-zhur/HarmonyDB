namespace HarmonyDB.Theory.Chords.Options;

/// <summary>
/// What a minus right after the root means when a degree that can be flattened follows it: "C-9", "C-6", "C-13".
/// A lone "C-" and "C-7" are minor, and "C-5" is C(b5), whatever this option says.
/// </summary>
public enum LeadingMinusMeaning
{
    /// <summary>
    /// The minus flattens the degree: "C-9" is C7(b9), "C-6" is C(b6).
    /// </summary>
    Flat,

    /// <summary>
    /// The minus is the minor: "C-9" is Cm9, "C-6" is Cm6.
    /// </summary>
    Minor,
}
