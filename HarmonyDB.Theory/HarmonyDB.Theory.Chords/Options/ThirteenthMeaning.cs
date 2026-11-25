namespace HarmonyDB.Theory.Chords.Options;

/// <summary>
/// Which degrees a thirteenth chord has below the thirteenth: "C13", "Cm13", "Cmaj13".
/// In practice the eleventh is left out (it clashes with the major third); in the full stack of thirds it is there.
/// </summary>
public enum ThirteenthMeaning
{
    /// <summary>
    /// The seventh and the ninth, without the eleventh: "C13" is C, E, G, Bb, D, A.
    /// </summary>
    WithoutEleventh,

    /// <summary>
    /// The seventh, the ninth and the eleventh: "C13" is C, E, G, Bb, D, F, A.
    /// </summary>
    WithEleventh,
}
