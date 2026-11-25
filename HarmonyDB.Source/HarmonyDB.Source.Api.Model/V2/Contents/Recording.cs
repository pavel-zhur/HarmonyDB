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
/// Sound playable through the music service: a track of a music service, or a file in its store. Exactly one of the two
/// is given.
/// </summary>
public record AudioRef
{
    public MusicServiceTrack? Track { get; init; }

    public BlobRef? Blob { get; init; }
}

public record MusicServiceTrack
{
    public required MusicService Service { get; init; }

    /// <summary>
    /// The track's id on the service.
    /// </summary>
    public required string Id { get; init; }
}

public enum MusicService
{
    Yandex,
    Youtube,
}

/// <summary>
/// A file in the music service's store.
/// </summary>
public record BlobRef
{
    public required string Key { get; init; }

    public required string ContentType { get; init; }
}
