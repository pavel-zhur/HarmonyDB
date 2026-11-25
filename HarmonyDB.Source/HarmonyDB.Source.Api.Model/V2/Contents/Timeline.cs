namespace HarmonyDB.Source.Api.Model.V2.Contents;

/// <summary>
/// The analysis of a recording: its keys, its bars with the times of their beats, the chords on them, its sections.
/// The music is linear: a repeat is written out.
/// </summary>
public record Timeline
{
    /// <summary>
    /// The length of the recording, seconds.
    /// </summary>
    public required double Duration { get; init; }

    /// <summary>
    /// The keys, in time order, one span per stretch in one key. Empty when no key was found.
    /// </summary>
    public required IReadOnlyList<KeySpan> Keys { get; init; }

    /// <summary>
    /// The bars in the order they sound. Empty when the material has chords at times only, with no bars.
    /// </summary>
    public required IReadOnlyList<Bar> Bars { get; init; }

    /// <summary>
    /// The chords in the order they begin. Each lasts until the next begins (or the end).
    /// </summary>
    public required IReadOnlyList<TimelineChord> Chords { get; init; }

    /// <summary>
    /// The parts of the song, in order; empty when not known.
    /// </summary>
    public required IReadOnlyList<Section> Sections { get; init; }
}

public record KeySpan
{
    /// <summary>
    /// A note and an optional "m" ("C", "F#m"); null for a stretch with no key.
    /// </summary>
    public string? Key { get; init; }

    public required double Start { get; init; }

    public required double End { get; init; }
}

/// <summary>
/// A bar, with the marks in force in it (a mark holds until the next one).
/// </summary>
public record Bar
{
    public required double Start { get; init; }

    /// <summary>
    /// Where the next bar begins; for the last bar, where the music ends.
    /// </summary>
    public required double End { get; init; }

    /// <summary>
    /// The times of the bar's own beats; the first is <see cref="Start"/>. A beat is what the meter is counted in:
    /// n/d has n beats of the note d (6/8, 9/8, 12/8: 2, 3, 4 dotted ones), a sum its groups, a free bar the moments
    /// marked in it. Only the first bar (a pickup) and the last may have fewer beats than their meter.
    /// </summary>
    public required IReadOnlyList<double> Beats { get; init; }

    /// <summary>
    /// The meter: "4/4", "12/8", "2+2+3/8" (groups of unequal length), or "free" (no pulse).
    /// </summary>
    public required string TimeSignature { get; init; }

    /// <summary>
    /// The tempo as a musician marks it; null in a free bar or when not known.
    /// </summary>
    public Tempo? Tempo { get; init; }

    /// <summary>
    /// The key: a note and an optional "m"; null for no key.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// The beats that are held (a fermata), by their numbers counted from 1.
    /// </summary>
    public IReadOnlyList<int> Fermatas { get; init; } = [];
}

public record Tempo
{
    /// <summary>
    /// Beats a minute of <see cref="BeatUnit"/>.
    /// </summary>
    public required double Bpm { get; init; }

    /// <summary>
    /// The note the tempo counts (a quarter in 4/4, a dotted quarter in 6/8, an eighth in 2+2+3/8).
    /// </summary>
    public required NoteValue BeatUnit { get; init; }
}

/// <summary>
/// A note's written length: 1 a whole, 2 a half, 4 a quarter, 8 an eighth, 16 a sixteenth; dotted or not.
/// </summary>
public record NoteValue
{
    public required int Denominator { get; init; }

    public bool Dotted { get; init; }
}

public record TimelineChord
{
    /// <summary>
    /// When it begins, seconds.
    /// </summary>
    public required double Start { get; init; }

    /// <summary>
    /// When the next one begins (or the music ends), seconds.
    /// </summary>
    public required double End { get; init; }

    public required ChordSymbol Symbol { get; init; }

    /// <summary>
    /// Its degree in the key in force ("I", "V / V", "bVII"), when the key is known.
    /// </summary>
    public string? Degree { get; init; }

    /// <summary>
    /// Its musical place, when the material places it in its bar.
    /// </summary>
    public ChordPlace? Place { get; init; }

    /// <summary>
    /// The pitches sounded, MIDI numbers (60 is middle C), from the lowest. Empty for no chord or a stop.
    /// </summary>
    public IReadOnlyList<int> Pitches { get; init; } = [];

    /// <summary>
    /// The notes as written, with their octaves ("A2", "C#4", "Bb3"), when the material writes them; these are
    /// <see cref="Pitches"/> spelled.
    /// </summary>
    public IReadOnlyList<string>? WrittenNotes { get; init; }

    /// <summary>
    /// The fingerings the material gives for it, one an instrument at most.
    /// </summary>
    public IReadOnlyList<Fingering> Fingerings { get; init; } = [];
}

/// <summary>
/// A chord's place in the music.
/// </summary>
public record ChordPlace
{
    /// <summary>
    /// The bar, by its index in <see cref="Timeline.Bars"/>, counted from 0.
    /// </summary>
    public required int Bar { get; init; }

    /// <summary>
    /// The beat of the bar, counted from 1.
    /// </summary>
    public required int Beat { get; init; }

    /// <summary>
    /// How far into the beat, an exact fraction ("1/2" the "and", "1/3" and "2/3" a triplet); null on the beat.
    /// </summary>
    public Fraction? At { get; init; }
}

public record Fraction
{
    public required int Numerator { get; init; }

    public required int Denominator { get; init; }
}

/// <summary>
/// What is written at a chord's place: a chord, no chord (the music goes on without one), or a stop (the band stops).
/// </summary>
public record ChordSymbol
{
    public required ChordSymbolKind Kind { get; init; }

    /// <summary>
    /// The chord's name with its bass ("Am7/G"), for <see cref="ChordSymbolKind.Chord"/> only.
    /// </summary>
    public string? Name { get; init; }
}

public enum ChordSymbolKind
{
    Chord,
    NoChord,
    Stop,
}

public record Fingering
{
    public required FingeringInstrument Instrument { get; init; }

    /// <summary>
    /// One number a string, from the lowest; -1 a string not played, 0 an open one.
    /// </summary>
    public required IReadOnlyList<int> Frets { get; init; }

    /// <summary>
    /// The finger on each string (1 the index … 4 the little finger, 0 none), as many as <see cref="Frets"/>, when given.
    /// </summary>
    public IReadOnlyList<int>? Fingers { get; init; }
}

public enum FingeringInstrument
{
    Guitar,
    Ukulele,
    Bass,
    Mandolin,
    Banjo,
}

public record Section
{
    public required double Start { get; init; }

    public required double End { get; init; }

    public required SectionLabel Label { get; init; }

    /// <summary>
    /// Which of its label it is ("Verse 2"), counted from 1; null when not counted.
    /// </summary>
    public int? Number { get; init; }

    /// <summary>
    /// The part of that one ("Verse 2b" is part 2), counted from 1; null when it is whole.
    /// </summary>
    public int? Part { get; init; }
}

public enum SectionLabel
{
    Intro,
    Verse,
    Chorus,
    Bridge,
    Solo,
    Instrumental,
    Break,
    Outro,
}
