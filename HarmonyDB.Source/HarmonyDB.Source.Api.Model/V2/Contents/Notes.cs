namespace HarmonyDB.Source.Api.Model.V2.Contents;

/// <summary>
/// The notes played, by part (an instrument, or one of the separate instruments of the recording), and the drums.
/// </summary>
public record Notes
{
    /// <summary>
    /// One part each; no two of one name.
    /// </summary>
    public required IReadOnlyList<NotesPart> Parts { get; init; }

    /// <summary>
    /// The drum hits, in time order.
    /// </summary>
    public IReadOnlyList<DrumHit> Drums { get; init; } = [];

    /// <summary>
    /// The same notes as a MIDI file, when there is one.
    /// </summary>
    public BlobRef? Midi { get; init; }
}

public record NotesPart
{
    /// <summary>
    /// The part's name as a person calls it ("Acoustic guitar", "Vocals").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// How it sounds: its General MIDI program, 0–127, when known.
    /// </summary>
    public int? Program { get; init; }

    /// <summary>
    /// The separate instrument of the recording the notes were heard in, when they were heard in one.
    /// </summary>
    public Stem? Stem { get; init; }

    /// <summary>
    /// The notes in the order they begin.
    /// </summary>
    public required IReadOnlyList<Note> Notes { get; init; }
}

public record Note
{
    public required double Start { get; init; }

    public required double End { get; init; }

    /// <summary>
    /// MIDI number, 60 is middle C.
    /// </summary>
    public required int Pitch { get; init; }

    /// <summary>
    /// 1–127, where it was heard.
    /// </summary>
    public int? Velocity { get; init; }
}

/// <summary>
/// A drum hit has no length and no pitch.
/// </summary>
public record DrumHit
{
    public required double Start { get; init; }

    /// <summary>
    /// Its General MIDI percussion key (36 kick, 38 snare, 42 closed hi-hat, …).
    /// </summary>
    public required int Drum { get; init; }

    public int? Velocity { get; init; }
}
