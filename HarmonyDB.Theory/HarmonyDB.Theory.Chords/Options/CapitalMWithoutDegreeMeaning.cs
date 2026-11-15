namespace HarmonyDB.Theory.Chords.Options;

/// <summary>
/// What a capital "M" means when no degree is written with it: "CM". With a degree ("CM7", "CM9") it is always the major seventh.
/// </summary>
public enum CapitalMWithoutDegreeMeaning
{
    /// <summary>
    /// The major seventh: "CM" is Cmaj7.
    /// </summary>
    MajorSeventh,

    /// <summary>
    /// The major triad: "CM" is C.
    /// </summary>
    Major,

    /// <summary>
    /// The minor, the capital written by mistake for "m": "CM" is Cm.
    /// </summary>
    Minor,
}
