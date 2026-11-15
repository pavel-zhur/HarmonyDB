namespace HarmonyDB.Theory.Chords.Options;

/// <summary>
/// What a bare "9" means when no seventh is written: "C9", "Am9". Authors write both meanings in the same sources about equally.
/// </summary>
public enum BareNinthMeaning
{
    /// <summary>
    /// The dominant ninth: "C9" is C, E, G, Bb, D.
    /// </summary>
    Dominant,

    /// <summary>
    /// The added ninth: "C9" is Cadd9.
    /// </summary>
    Added,

    /// <summary>
    /// Decided for each such chord of a song: added when its seventh is out of the key the other chords make, dominant otherwise.
    /// Parsing needs the song's chords (<see cref="Parsers.ChordParser.ParseSong"/>).
    /// </summary>
    Detect,
}
