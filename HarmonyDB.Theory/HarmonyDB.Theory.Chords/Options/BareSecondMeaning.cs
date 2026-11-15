namespace HarmonyDB.Theory.Chords.Options;

/// <summary>
/// What a bare "2" means when no third is written: "C2", "C7/2".
/// </summary>
public enum BareSecondMeaning
{
    /// <summary>
    /// The second is added to the triad: "C2" is Cadd9 (the third stays).
    /// </summary>
    Added,

    /// <summary>
    /// The second replaces the third: "C2" is Csus2.
    /// </summary>
    Suspended,
}
