namespace HarmonyDB.Theory.Chords.Options;

/// <summary>
/// Whether a notation that collides with others is read.
/// </summary>
public enum NotationUse
{
    Never,
    Always,

    /// <summary>
    /// Decided for each song from its chords; parsing needs the song's chords (<see cref="Parsers.ChordParser.ParseSong"/>).
    /// </summary>
    Detect,
}
