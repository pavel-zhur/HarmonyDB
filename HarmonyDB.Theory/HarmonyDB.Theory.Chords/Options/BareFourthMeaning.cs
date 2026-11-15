namespace HarmonyDB.Theory.Chords.Options;

/// <summary>
/// What a bare "4" means when no third is written: "C4", "C7/4", "C4/7". With a written third ("Cm4") the fourth is always added.
/// </summary>
public enum BareFourthMeaning
{
    /// <summary>
    /// The fourth replaces the third: "C4" is Csus4.
    /// </summary>
    Suspended,

    /// <summary>
    /// The fourth is added to the triad: "C4" is Cadd11 (the third stays).
    /// </summary>
    Added,
}
