namespace HarmonyDB.Theory.Chords.Options;

/// <summary>
/// What "dim" means when no seventh is written: "Cdim". The symbols "o", "°", "º" are always the diminished seventh chord.
/// </summary>
public enum DimWithoutSeventhMeaning
{
    /// <summary>
    /// The diminished triad: "Cdim" is C, Eb, Gb.
    /// </summary>
    Triad,

    /// <summary>
    /// The diminished seventh chord: "Cdim" is C, Eb, Gb, A.
    /// </summary>
    Seventh,
}
