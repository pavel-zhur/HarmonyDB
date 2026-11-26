namespace HarmonyDB.Source.Api.Model.V2.Contents;

/// <summary>
/// The recording the material was made from, and its separate instruments, all sample-aligned with it.
/// </summary>
public record Recording
{
    public required AudioRef Audio { get; init; }

    /// <summary>
    /// Its length, seconds.
    /// </summary>
    public required double Duration { get; init; }

    /// <summary>
    /// The separate instruments, one each; empty when it was not separated.
    /// </summary>
    public IReadOnlyList<StemAudio> Stems { get; init; } = [];
}

public record StemAudio
{
    public required Stem Stem { get; init; }

    public required AudioRef Audio { get; init; }
}

/// <summary>
/// A separate instrument of a recording.
/// </summary>
public enum Stem
{
    Vocals,
    Guitar,
    Bass,
    Drums,
    Piano,
    Other,
}

/// <summary>
/// Sound: a track of a music service, or a file of the item. Exactly one of the two is given.
/// </summary>
public record AudioRef
{
    public MusicServiceTrack? Track { get; init; }

    public FileRef? File { get; init; }
}

/// <summary>
/// A track of a music service: a fact about the world, stated by the source.
/// </summary>
public record MusicServiceTrack
{
    /// <summary>
    /// The music service, in lower case ("yandex", "youtube", or any other). A reader plays the services it knows and
    /// passes over the rest.
    /// </summary>
    public required string Service { get; init; }

    /// <summary>
    /// The track's id on the service.
    /// </summary>
    public required string Id { get; init; }
}

/// <summary>
/// A file of the item, given by its source (<see cref="SourceV2Urls.V2GetFile"/>) to whom the item is open, while the
/// item's content names it.
/// </summary>
public record FileRef
{
    /// <summary>
    /// Its name: opaque to all but the source, unique within its item.
    /// </summary>
    public required string Name { get; init; }

    public required string ContentType { get; init; }
}