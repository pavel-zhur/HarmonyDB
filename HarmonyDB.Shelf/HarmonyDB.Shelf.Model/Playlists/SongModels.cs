using System.Diagnostics.CodeAnalysis;
using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Shelf.Model.Me;

namespace HarmonyDB.Shelf.Model.Playlists;

public enum SongSort
{
    /// <summary>The playlist's own order (a playlist only).</summary>
    Order,

    /// <summary>By how many people marked the song's chords (of those whose marks the caller sees).</summary>
    Popular,

    /// <summary>By the artists, then the title.</summary>
    Artist,

    Title,

    /// <summary>The songs added last first.</summary>
    NewSongs,

    /// <summary>The songs whose chords were added last first.</summary>
    NewChords,

    /// <summary>The songs the caller marked last first.</summary>
    MyMarks,
}

/// <summary>
/// One row of a list of songs (the library, a playlist): a song of a playlist, or, in the library, chords the person
/// marked or keeps a key or a note on that are in none of the playlists shown there.
/// </summary>
public record SongRow
{
    /// <summary>
    /// The song; null for chords alone.
    /// </summary>
    public Guid? SongId { get; init; }

    /// <summary>
    /// The song's playlist; null for chords alone.
    /// </summary>
    public Guid? PlaylistId { get; init; }

    /// <summary>
    /// In the library: the other shown playlists that hold some of the same chords (the row stands for them too).
    /// </summary>
    public IReadOnlyList<Guid>? AlsoIn { get; init; }

    /// <summary>
    /// Chords alone: the chords.
    /// </summary>
    public ChordsAnswer? Chords { get; init; }

    public required string Title { get; init; }

    public required IReadOnlyList<string> Artists { get; init; }

    /// <summary>
    /// The caller's marks on the song's chords: the kinds, the latest first.
    /// </summary>
    public required IReadOnlyList<string> MyMarks { get; init; }

    /// <summary>
    /// Others' marks the caller sees on the song's chords: the kinds, the most used first, at most three.
    /// </summary>
    public required IReadOnlyList<string> OtherMarks { get; init; }

    /// <summary>
    /// How many other people marked the song's chords.
    /// </summary>
    public required int OtherPeople { get; init; }

    /// <summary>
    /// The sources of the song's chords open to the caller, as their sources give them to the caller, at most three
    /// different ones.
    /// </summary>
    public required IReadOnlyList<ChordsSource> Sources { get; init; }

    public required int ChordsCount { get; init; }

    /// <summary>
    /// The ids of the chords of its variants, in their order (to show a mark made a moment ago).
    /// </summary>
    public required IReadOnlyList<string> ChordsIds { get; init; }

    /// <summary>
    /// Its cover, an address the browser loads; none when it has none.
    /// </summary>
    public string? Cover { get; init; }

    public required int RecordingsCount { get; init; }

    public required DateTimeOffset AddedAt { get; init; }
}

public record SongList
{
    /// <summary>
    /// The rows that match, in all.
    /// </summary>
    public required int Total { get; init; }

    public required int Offset { get; init; }

    public required IReadOnlyList<SongRow> Rows { get; init; }

    /// <summary>
    /// The people whose marks the caller sees here (for the filter by person), the caller first.
    /// </summary>
    public required IReadOnlyList<PersonRef> People { get; init; }
}

/// <summary>
/// A song just added; a copy of a song takes only the variants open to the person: how many were left out.
/// </summary>
public record AddedSong : SongRef
{
    public AddedSong()
    {
    }

    [SetsRequiredMembers]
    public AddedSong(SongRef song)
        : base(song)
    {
    }

    public int LeftOut { get; init; }
}

public record SongRef
{
    public required Guid Id { get; init; }

    public required Guid PlaylistId { get; init; }

    public required string Title { get; init; }

    public required IReadOnlyList<string> Artists { get; init; }

    public required double Position { get; init; }

    public PersonRef? AddedBy { get; init; }

    public required DateTimeOffset AddedAt { get; init; }
}

/// <summary>
/// A chords variant of a song. Its letter is <see cref="Index"/> (0: A …): the same for everyone looking at the song.
/// </summary>
public record ChordsVariant
{
    public required Guid ItemId { get; init; }

    public required int Index { get; init; }

    public required ChordsAnswer Chords { get; init; }

    public PersonRef? AddedBy { get; init; }

    public required DateTimeOffset AddedAt { get; init; }

    /// <summary>
    /// How many of the caller's playlists hold these chords.
    /// </summary>
    public required int InMyPlaylists { get; init; }
}

public record RecordingRef
{
    public required Guid Id { get; init; }

    public required MusicService Service { get; init; }

    /// <summary>
    /// The track's id on the service.
    /// </summary>
    public required string ServiceId { get; init; }

    public string? Title { get; init; }

    public required IReadOnlyList<string> Artists { get; init; }

    public double? DurationSeconds { get; init; }

    public string? CoverUrl { get; init; }
}

public record RecordingVariant
{
    public required Guid ItemId { get; init; }

    public required RecordingRef Recording { get; init; }

    public PersonRef? AddedBy { get; init; }

    public required DateTimeOffset AddedAt { get; init; }
}

public record MarkItem
{
    public required Guid AccountId { get; init; }

    /// <summary>
    /// The chords' id.
    /// </summary>
    public required string Chords { get; init; }

    public required string Kind { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

public record KeyItem
{
    public required Guid AccountId { get; init; }

    public required string Chords { get; init; }

    /// <summary>
    /// −11 … +11.
    /// </summary>
    public required int Semitones { get; init; }

    /// <summary>
    /// "Here" (in this playlist) or "mine" (everywhere).
    /// </summary>
    public required bool Here { get; init; }
}

public record SongNote
{
    public required Guid AccountId { get; init; }

    public required string Text { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}

public record ChordsNote
{
    public required string Chords { get; init; }

    public required string Text { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// A song's page: the song, its variants with their letters, the marks and keys the caller sees on them, the notes.
/// Others' marks and keys are seen by the playlist's members only; a guest sees none, not even their own.
/// </summary>
public record SongPage
{
    public required SongRef Song { get; init; }

    /// <summary>
    /// The playlist; null when the song was opened by its link and the playlist is private.
    /// </summary>
    public PlaylistSummary? Playlist { get; init; }

    /// <summary>
    /// Opened by its link, from a private playlist: the playlist is not shown.
    /// </summary>
    public required bool FromPrivatePlaylist { get; init; }

    public required bool CanAdd { get; init; }

    /// <summary>
    /// Whether the caller may rename, move, remove the song and link and unlink its variants (its adder, or an admin).
    /// </summary>
    public required bool CanEdit { get; init; }

    public required IReadOnlyList<ChordsVariant> Variants { get; init; }

    public required IReadOnlyList<RecordingVariant> Recordings { get; init; }

    public Guid? MyMainItemId { get; init; }

    public Guid? MyRecordingItemId { get; init; }

    /// <summary>
    /// Everyone named in the marks, keys and notes.
    /// </summary>
    public required IReadOnlyList<PersonRef> People { get; init; }

    public required IReadOnlyList<MarkItem> Marks { get; init; }

    public required IReadOnlyList<KeyItem> Keys { get; init; }

    /// <summary>
    /// The notes in the playlist on this song (members read them).
    /// </summary>
    public required IReadOnlyList<SongNote> Notes { get; init; }

    /// <summary>
    /// The caller's personal notes on the variants' chords.
    /// </summary>
    public required IReadOnlyList<ChordsNote> MyNotes { get; init; }
}

/// <summary>
/// A song to add to a playlist: chords (by the source's id), or a copy of a song the caller sees (its title, artists and
/// variants).
/// </summary>
public record AddSongRequest
{
    /// <summary>
    /// The chords of the new song.
    /// </summary>
    public string? Chords { get; init; }

    /// <summary>
    /// The song's title in this playlist (with <see cref="Chords"/>); not given: the chords' title as last seen.
    /// </summary>
    public string? Title { get; init; }

    public IReadOnlyList<string>? Artists { get; init; }

    /// <summary>
    /// A song to copy.
    /// </summary>
    public Guid? FromSongId { get; init; }

    /// <summary>
    /// The link the song to copy was opened by, if it was.
    /// </summary>
    public string? Share { get; init; }
}

public record UpdateSongRequest
{
    public string? Title { get; init; }

    public IReadOnlyList<string>? Artists { get; init; }

    /// <summary>
    /// Move the song right after this one (<see cref="Guid.Empty"/>: to the top).
    /// </summary>
    public Guid? After { get; init; }
}

public record AddVariantRequest
{
    public required string Chords { get; init; }
}

public record LinkSongsRequest
{
    /// <summary>
    /// The songs of the same playlist whose variants join this song; they are removed.
    /// </summary>
    public required IReadOnlyList<Guid> SongIds { get; init; }

    /// <summary>
    /// The name of the song they make, in this playlist; not given: this song's.
    /// </summary>
    public string? Title { get; init; }

    public IReadOnlyList<string>? Artists { get; init; }
}

public record SetMainVariantRequest
{
    /// <summary>
    /// Null: none chosen.
    /// </summary>
    public Guid? ItemId { get; init; }
}
