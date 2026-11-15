namespace HarmonyDB.Theory.Chords.Options;

/// <summary>
/// What "maj" means when no degree is written with it: "Cmaj", "Cmaj5-", "Cmajadd11+", "Cmaj(9)".
/// </summary>
public enum MajWithoutDegreeMeaning
{
    /// <summary>
    /// The major seventh: "Cmaj" is Cmaj7.
    /// </summary>
    MajorSeventh,

    /// <summary>
    /// The major triad: "Cmaj" is C.
    /// </summary>
    Major,
}
