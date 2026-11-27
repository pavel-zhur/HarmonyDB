using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Model.Chords;

/// <summary>
/// What became of an item of chords asked for this person.
/// </summary>
public enum ChordsState
{
    /// <summary>Open to the person: its header (and content) is given.</summary>
    Open,

    /// <summary>Not open to the person (or not known, or the source could not give it). Nothing else of it is said but
    /// its address in the old shelf, where the shelf keeps one: the app shows "could not be loaded", with that address.</summary>
    NotOpen,

    /// <summary>Its source did not answer now. Not "not open": try later.</summary>
    Unavailable,
}

/// <summary>
/// The kinds of content an item of chords may have (the source contract's kinds).
/// </summary>
public enum ChordsKind
{
    /// <summary>Text with chords; every item has it.</summary>
    Sheet,

    /// <summary>The analysis of a recording: keys, bars and beats, chords on them, sections.</summary>
    Timeline,

    /// <summary>The words by lines and words, with their times.</summary>
    Lyrics,

    /// <summary>The notes played, by instrument.</summary>
    Notes,

    /// <summary>A score (MusicXML) with its times.</summary>
    Score,

    /// <summary>Chord charts (iReal Pro and others).</summary>
    Chart,

    /// <summary>The recording the material was made from, and its separate instruments.</summary>
    Recording,
}

/// <summary>
/// A source as it is shown to this person (of an item: where it comes from): it may differ between people.
/// </summary>
public sealed record ChordsSource
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    /// <summary>
    /// The source's picture (<c>GET /api/chords/icons/{name}</c>), when it has one.
    /// </summary>
    public string? IconUrl { get; init; }
}

/// <summary>
/// What the source states about the item.
/// </summary>
public sealed record ChordsAttributes
{
    /// <summary>The key, a note and an optional "m" ("C", "F#m").</summary>
    public string? Key { get; init; }

    public double? Bpm { get; init; }

    public int? Capo { get; init; }

    /// <summary>"4/4", "6/8".</summary>
    public string? TimeSignature { get; init; }
}

/// <summary>
/// What the source marks about the item, where it marks it.
/// </summary>
public sealed record ChordsMarks
{
    /// <summary>The source vouches for the item as one of its best.</summary>
    public bool IsChosen { get; init; }

    /// <summary>The source's words for that mark, when it has them.</summary>
    public string? ChosenText { get; init; }

    /// <summary>Whether the source fully supports the item's kind (<see cref="ChordsHeader.Type"/>); null when it does
    /// not say.</summary>
    public bool? IsSupported { get; init; }
}

public sealed record ChordsHeader
{
    /// <summary>
    /// The item's id at its source: opaque, the same for everyone.
    /// </summary>
    public required string Id { get; init; }

    public required ChordsSource Source { get; init; }

    /// <summary>
    /// The item's page at its source, when given to this person. A link to it opens the item to whoever holds it where
    /// its source says so (<c>GET /api/chords/{id}?address=</c>).
    /// </summary>
    public string? Address { get; init; }

    public string? Title { get; init; }

    public required IReadOnlyList<string> Artists { get; init; }

    public float? Rating { get; init; }

    /// <summary>The source's own type of the item, as the source names it.</summary>
    public string? Type { get; init; }

    public required IReadOnlyList<ChordsKind> Kinds { get; init; }

    public required ChordsAttributes Attributes { get; init; }

    public required ChordsMarks Marks { get; init; }

    /// <summary>Whether the item may be shown on public pages.</summary>
    public required bool IsPublic { get; init; }
}

/// <summary>
/// Chords as the shelf keeps them, without asking their source: the id, the address they are read by, and what their
/// source last gave of them on a reading by id (the same for everyone). The source's label is the person's own: the
/// device keeps it (<c>POST /api/chords/labels</c>).
/// </summary>
public sealed record ChordsRef
{
    public required string Id { get; init; }

    /// <summary>
    /// The address the chords were added by, passed on with every reading of them (<c>GET /api/chords/{id}?address=</c>).
    /// </summary>
    public string? Address { get; init; }

    public string? Title { get; init; }

    public required IReadOnlyList<string> Artists { get; init; }

    /// <summary>The kinds of content they have, as last read; empty when never read.</summary>
    public required IReadOnlyList<ChordsKind> Kinds { get; init; }
}

public sealed record ChordsLabelsRequest
{
    /// <summary>
    /// The items' ids at their sources, each once, at most 1000.
    /// </summary>
    public required IReadOnlyList<string> Ids { get; init; }
}

public sealed record ChordsLabel
{
    public required string Id { get; init; }

    public required ChordsSource Source { get; init; }
}

public sealed record ChordsLabelsResponse
{
    /// <summary>
    /// The source of every id its source knows, as shown to this person, in the order asked; an id no source knows, or
    /// whose source did not answer, is left out.
    /// </summary>
    public required IReadOnlyList<ChordsLabel> Labels { get; init; }
}

/// <summary>
/// One item asked: its state for this person, and its header when it is open.
/// </summary>
public sealed record ChordsAnswer
{
    public required string Id { get; init; }

    public required ChordsState State { get; init; }

    public ChordsHeader? Header { get; init; }

    /// <summary>
    /// Not open: the item's address in the old shelf, when the shelf keeps one — where the person may look at it.
    /// </summary>
    public string? Address { get; init; }
}

public sealed record ChordsHeadersRequest
{
    /// <summary>
    /// The items' ids at their sources, at most 200.
    /// </summary>
    public required IReadOnlyList<string> Ids { get; init; }
}

public sealed record ChordsHeadersResponse
{
    /// <summary>
    /// One answer for every id asked, in the order asked.
    /// </summary>
    public required IReadOnlyList<ChordsAnswer> Items { get; init; }

    /// <summary>
    /// The person's own marks, keys ("mine") and personal notes on these chords, each naming its chords' id; empty for
    /// a guest. (In a playlist, its song page gives the members' too.)
    /// </summary>
    public required IReadOnlyList<MarkItem> Marks { get; init; }

    public required IReadOnlyList<KeyItem> Keys { get; init; }

    public required IReadOnlyList<ChordsNote> MyNotes { get; init; }
}

/// <summary>
/// An item with the kinds of content asked. The chord sheet comes whole, as the source gave it: transposition,
/// accidentals, simplification, degrees and the other rendering options are applied by the app.
/// </summary>
public sealed record ChordsContentResponse
{
    public required string Id { get; init; }

    public required ChordsState State { get; init; }

    /// <summary>
    /// The shelf's own id of the item (what marks, keys and notes are kept on); given when it is open.
    /// </summary>
    public Guid? ChordsId { get; init; }

    public ChordsHeader? Header { get; init; }

    /// <summary>
    /// Not open: the item's address in the old shelf, when the shelf keeps one — where the person may look at it.
    /// </summary>
    public string? Address { get; init; }

    /// <summary>
    /// The kinds asked that the item has, in the source contract's (version 2) own models and JSON: one property per
    /// kind (<c>sheet</c>, <c>timeline</c> …), null for a kind not asked or not had. Given when it is open.
    /// </summary>
    public Content? Content { get; init; }

    /// <summary>
    /// The person's own marks, keys ("mine") and personal notes on these chords; empty for a guest or when the chords are
    /// not open. (In a playlist, its song page gives the members' too.)
    /// </summary>
    public required IReadOnlyList<MarkItem> Marks { get; init; }

    public required IReadOnlyList<KeyItem> Keys { get; init; }

    public required IReadOnlyList<ChordsNote> MyNotes { get; init; }
}

/// <summary>
/// The sources this person may see: their labels, as a search could show them.
/// </summary>
public sealed record ChordsSourcesResponse
{
    public required IReadOnlyList<ChordsSource> Sources { get; init; }

    /// <summary>
    /// Some source did not answer: the list may lack its labels.
    /// </summary>
    public required bool Incomplete { get; init; }
}
