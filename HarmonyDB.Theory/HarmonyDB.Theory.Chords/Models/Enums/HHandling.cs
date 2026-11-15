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

    /// <summary>
    /// Decided for each song from its chords: German (<see cref="BbMeansH"/>) when the song uses H or German note names,
    /// or when its chords with the bare B make sense only as B flat; English (<see cref="BMeansH"/>) otherwise.
    /// Parsing needs the song's chords (<see cref="Parsers.ChordParser.ParseSong"/>).
    /// </summary>
    Detect,
}