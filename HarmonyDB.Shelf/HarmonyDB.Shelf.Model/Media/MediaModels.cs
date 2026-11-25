using HarmonyDB.Shelf.Model.Me;

namespace HarmonyDB.Shelf.Model.Media;

public enum MediaKind
{
    /// <summary>The track's sound.</summary>
    Audio,

    /// <summary>The track's video (YouTube; on Yandex, its background loop).</summary>
    Video,
}

public sealed record TrackMediaRequest
{
    public required MusicService Service { get; init; }

    /// <summary>The track's id on the service.</summary>
    public required string Id { get; init; }

    public required MediaKind Kind { get; init; }
}

/// <summary>
/// A short-lived address of sound or video, played with Range requests. It works for anyone holding it until it
/// expires; then a new one is asked for.
/// </summary>
public sealed record MediaAddress
{
    /// <summary><c>/api/media/…</c>.</summary>
    public required string Url { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

public sealed record ChordsMediaRequest
{
    /// <summary>The item's id at its source.</summary>
    public required string Id { get; init; }
}

/// <summary>
/// A separate instrument of a recording.
/// </summary>
public enum MediaStem
{
    Vocals,
    Guitar,
    Bass,
    Drums,
    Piano,
    Other,
}

public sealed record StemMedia
{
    public required MediaStem Stem { get; init; }

    public required string Url { get; init; }
}

/// <summary>
/// The sound of the material of an item of chords (an analysis): the recording it was made from, its separate
/// instruments, its notes as a MIDI file — whichever it has. All the addresses expire together.
/// </summary>
public sealed record ChordsMedia
{
    public string? RecordingUrl { get; init; }

    public double? DurationSeconds { get; init; }

    public required IReadOnlyList<StemMedia> Stems { get; init; }

    public string? MidiUrl { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
