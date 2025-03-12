namespace HarmonyDB.Theory.Chords.Models.Enums;

public enum HHandling
{
    /// <summary>
    /// English variant. B = H = Si = A##. A# = Bb.
    /// </summary>
    BMeansH,

    /// <summary>
    /// German variant. B = A#. H = B# = Si.
    /// </summary>
    BbMeansH,

    /// <summary>
    /// Strict English variant. H is prohibited.
    /// </summary>
    HProhibited,
}