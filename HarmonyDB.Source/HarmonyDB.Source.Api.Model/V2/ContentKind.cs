namespace HarmonyDB.Source.Api.Model.V2;

/// <summary>
/// The kinds of content an item may have. Each is a typed model of <see cref="Content"/>.
/// </summary>
public enum ContentKind
{
    /// <summary>Text with chords.</summary>
    Sheet,

    /// <summary>The analysis of a recording: keys, bars and beats, chords on them, sections.</summary>
    Timeline,

    /// <summary>The words, by lines and words, with their times.</summary>
    Lyrics,

    /// <summary>The notes played, by instrument, and the drums.</summary>
    Notes,

    /// <summary>A score in MusicXML, with its times on the recording.</summary>
    Score,

    /// <summary>Chord charts: a band chart, iReal Pro.</summary>
    Chart,

    /// <summary>The recording the material was made from, and its separate instruments.</summary>
    Recording,
}
