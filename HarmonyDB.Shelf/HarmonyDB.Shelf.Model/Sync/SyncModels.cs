using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Model.Sync;

public enum ChangeType
{
    /// <summary>A mark put (<see cref="Change.On"/>) or taken off: <see cref="Change.Chords"/>, <see cref="Change.Kind"/>.</summary>
    Mark,

    /// <summary>A key set or removed (<see cref="Change.Semitones"/> null): "mine" on <see cref="Change.Chords"/>, or
    /// "here" with <see cref="Change.PlaylistId"/>.</summary>
    Key,

    /// <summary>A note written or removed (<see cref="Change.Text"/> null or empty): personal on
    /// <see cref="Change.Chords"/>, or in a playlist on <see cref="Change.SongId"/>.</summary>
    Note,

    /// <summary>Opened: <see cref="Change.Chords"/>, and the song they were opened in (<see cref="Change.SongId"/>) if
    /// they were; when (<see cref="Change.At"/>).</summary>
    Opened,
}

/// <summary>
/// A small change made in the browser at once and queued (offline, or before the server answered).
/// </summary>
public record Change
{
    public required ChangeType Type { get; init; }

    /// <summary>
    /// The chords' id.
    /// </summary>
    public string? Chords { get; init; }

    public string? Kind { get; init; }

    public bool? On { get; init; }

    public Guid? PlaylistId { get; init; }

    public Guid? SongId { get; init; }

    public int? Semitones { get; init; }

    public string? Text { get; init; }

    public DateTimeOffset? At { get; init; }

    /// <summary>
    /// Opened: the chords' title, artists and source as the person was shown them, kept with their recent row.
    /// </summary>
    public string? Title { get; init; }

    public IReadOnlyList<string>? Artists { get; init; }

    public ChordsSource? Source { get; init; }
}

public record SyncRequest
{
    /// <summary>
    /// The account the browser's queue belongs to: a batch of another account than the session's is refused (409
    /// <c>wrong_account</c>), so a tab left open under one person never sends its queue as another.
    /// </summary>
    public required Guid AccountId { get; init; }

    /// <summary>
    /// The watermark of the last answer the browser holds; null: everything.
    /// </summary>
    public long? Since { get; init; }

    /// <summary>
    /// At most 500, applied in their order.
    /// </summary>
    public IReadOnlyList<Change> Changes { get; init; } = [];
}

public record ChangeResult
{
    public required bool Applied { get; init; }

    /// <summary>
    /// Why it was not applied, a code.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// The token that undoes it, for a short while; null when it changed nothing.
    /// </summary>
    public string? Undo { get; init; }
}

public record MyMark
{
    public required string Chords { get; init; }

    public required string Kind { get; init; }

    /// <summary>
    /// False: taken off.
    /// </summary>
    public required bool On { get; init; }

    public required DateTimeOffset At { get; init; }
}

public record MyKey
{
    public required string Chords { get; init; }

    /// <summary>
    /// Null: "mine".
    /// </summary>
    public Guid? PlaylistId { get; init; }

    /// <summary>
    /// Null: removed.
    /// </summary>
    public int? Semitones { get; init; }
}

public record MyNote
{
    public string? Chords { get; init; }

    public Guid? SongId { get; init; }

    /// <summary>
    /// Null: removed.
    /// </summary>
    public string? Text { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}

public record MyRecent
{
    public string? Chords { get; init; }

    public Guid? SongId { get; init; }

    public required DateTimeOffset OpenedAt { get; init; }
}

/// <summary>
/// The results of the changes sent, in their order, and what changed since <see cref="SyncRequest.Since"/>: the
/// person's own marks, keys, notes and recent (the removed ones too, when asked since a watermark), and the playlists
/// whose content the person sees changed (to read anew).
/// </summary>
public record SyncResponse
{
    public required IReadOnlyList<ChangeResult> Results { get; init; }

    /// <summary>
    /// To send as <see cref="SyncRequest.Since"/> next time.
    /// </summary>
    public required long Watermark { get; init; }

    public required IReadOnlyList<MyMark> Marks { get; init; }

    public required IReadOnlyList<MyKey> Keys { get; init; }

    public required IReadOnlyList<MyNote> Notes { get; init; }

    public required IReadOnlyList<MyRecent> Recent { get; init; }

    public required IReadOnlyList<Guid> Playlists { get; init; }
}

public record UndoRequest
{
    public required string Token { get; init; }
}

public record RecentEntry
{
    public ChordsRef? Chords { get; init; }

    /// <summary>
    /// The song they were opened in, when it is still there and the person still sees it.
    /// </summary>
    public SongRef? Song { get; init; }

    /// <summary>
    /// What it was seen under: the title, artists and source the person was shown when they opened it; for a row from
    /// before these were kept, the chords' kept title, else the song's, and no source.
    /// </summary>
    public string? Title { get; init; }

    public required IReadOnlyList<string> Artists { get; init; }

    public ChordsSource? Source { get; init; }

    /// <summary>
    /// The song's cover, an address the browser loads.
    /// </summary>
    public string? Cover { get; init; }

    public required DateTimeOffset OpenedAt { get; init; }
}
