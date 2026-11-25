using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Source.Api.Model.V2;

namespace HarmonyDB.Shelf.Model.Playlists;

/// <summary>
/// The chords of the person's library, for a device that keeps the library to open it without a connection
/// (<c>GET /api/library/offline/chords</c>). The playlists, their songs and the person's marks, keys and notes come by
/// the sync (<c>POST /api/sync</c>) and the playlists' own reads, as for the app online.
/// </summary>
public record OfflineChordsList
{
    /// <summary>
    /// To send as <c>since</c> next time.
    /// </summary>
    public required long Watermark { get; init; }

    /// <summary>
    /// False: nothing that makes up the library changed since the watermark sent, and <see cref="Chords"/> is empty.
    /// True: <see cref="Chords"/> is the whole list; what the device holds and is not in it has left the library.
    /// </summary>
    public required bool Changed { get; init; }

    /// <summary>
    /// The ids of all the chords of the library (of the playlists shown in it, and the person's own marked ones or
    /// those with their key or note), whether open to the person or not: the content read says which are.
    /// </summary>
    public required IReadOnlyList<string> Chords { get; init; }
}

/// <summary>
/// The content of chords of the library, for a device to keep (<c>POST /api/library/offline/contents</c>).
/// </summary>
public record OfflineContentsRequest
{
    /// <summary>
    /// The most chords one call may ask for.
    /// </summary>
    public const int MaxChords = 50;

    /// <summary>
    /// The kinds kept offline: the text with chords and what the song page shows with it, tens of kilobytes an item. The
    /// notes and the score (hundreds of kilobytes to megabytes an item) and the recording (media) are not.
    /// </summary>
    public static readonly IReadOnlySet<ChordsKind> KeptKinds = new HashSet<ChordsKind> { ChordsKind.Sheet, ChordsKind.Timeline, ChordsKind.Lyrics, ChordsKind.Chart };

    /// <summary>
    /// Ids of chords of the person's library, each once; at most <see cref="MaxChords"/>.
    /// </summary>
    public required IReadOnlyList<string> Chords { get; init; }

    /// <summary>
    /// The kinds wanted, of <see cref="KeptKinds"/> (sheet, timeline, lyrics, chart); the sheet when none is given.
    /// Another kind (notes, score, recording) is refused: 400.
    /// </summary>
    public IReadOnlyList<ChordsKind>? Kinds { get; init; }
}

public record OfflineContents
{
    /// <summary>
    /// One for every id asked, in the order asked.
    /// </summary>
    public required IReadOnlyList<OfflineContent> Items { get; init; }
}

public enum OfflineContentState
{
    /// <summary>Open to the person: its header and the kinds asked that it has are given.</summary>
    Open,

    /// <summary>Not open to the person (or not known): the device drops what it keeps of it.</summary>
    NotOpen,

    /// <summary>Its source did not answer now: the device keeps what it has and asks again later.</summary>
    Unavailable,

    /// <summary>Not in the person's library: nothing is read for it.</summary>
    NotInLibrary,
}

/// <summary>
/// The chords as the song page shows them without a connection: the header and the content, in the source contract's
/// own models (as <c>GET /api/chords/{id}</c> gives them). Media — the recording, separate instruments, the files the
/// content names, pictures — are not part of it.
/// </summary>
public record OfflineContent
{
    public required string Chords { get; init; }

    public required OfflineContentState State { get; init; }

    /// <summary>
    /// Given when it is open.
    /// </summary>
    public ChordsHeader? Header { get; init; }

    /// <summary>
    /// The kinds asked that it has; given when it is open.
    /// </summary>
    public Content? Content { get; init; }
}
