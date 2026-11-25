using HarmonyDB.Source.Api.Model.V2;

namespace HarmonyDB.Shelf.Api.Areas.Analyses;

/// <summary>
/// An analysis of one recording, put by an internal client (<c>PUT /internal/analyses</c>), written with the source
/// contract's JSON options. Put again with the same <see cref="Key"/>, it replaces what that client put before.
/// </summary>
public sealed record PutAnalysisRequest
{
    /// <summary>
    /// The client's own stable name of the analysis, 1–200 characters.
    /// </summary>
    public required string Key { get; init; }

    public required string Title { get; init; }

    public IReadOnlyList<string> Artists { get; init; } = [];

    /// <summary>
    /// The account it is open to, when it is of that person's own file; null: open to all.
    /// </summary>
    public Guid? OwnerId { get; init; }

    /// <summary>
    /// Its content in the source contract's kinds, the sheet left out (it is made here of the timeline and the words).
    /// The timeline is required. A file of the analysis (<see cref="Source.Api.Model.V2.Contents.BlobRef"/>: the
    /// recording, a separate instrument, a MIDI file) is named by its <c>Key</c> as the file's name, put next with
    /// <c>PUT /internal/analyses/{id}/files/{name}</c> in the content type given here.
    /// </summary>
    public required Content Content { get; init; }
}

public sealed record PutAnalysisResponse
{
    /// <summary>
    /// Its id as a source's item.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The files its content names, to be put.
    /// </summary>
    public required IReadOnlyList<string> Files { get; init; }
}

/// <summary>
/// A file of an analysis, as the music service keeps it.
/// </summary>
public sealed record AnalysisFileInfo
{
    public required string Name { get; init; }

    public required string ContentType { get; init; }

    public required long Bytes { get; init; }

    /// <summary>
    /// The SHA-256 of its content, hex.
    /// </summary>
    public required string Sha256 { get; init; }
}

public sealed record PutAnalysisFileResponse
{
    public required string Name { get; init; }

    public required long Bytes { get; init; }
}

/// <summary>
/// A public playlist of a system account that holds these analyses, in this order (<c>PUT /internal/analyses/playlist</c>).
/// Put again, the playlist is found by its account and title and made to hold exactly these.
/// </summary>
public sealed record PublishAnalysesPlaylistRequest
{
    /// <summary>
    /// The system account's stable name, also its display name; made when there is none.
    /// </summary>
    public required string SystemAccount { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    /// <summary>
    /// The analyses' ids, each open to all.
    /// </summary>
    public required IReadOnlyList<string> Ids { get; init; }
}

public sealed record PublishAnalysesPlaylistResponse
{
    public required Guid PlaylistId { get; init; }

    public required Guid AccountId { get; init; }

    public required int Songs { get; init; }

    public required int Added { get; init; }

    public required int Removed { get; init; }
}

/// <summary>
/// The error codes of the analyses' internal endpoints.
/// </summary>
public static class AnalysesErrorCodes
{
    /// <summary>400: the content has no timeline.</summary>
    public const string TimelineMissing = "timeline_missing";

    /// <summary>400: the content gives a sheet; the sheet is made here.</summary>
    public const string SheetGiven = "sheet_given";

    /// <summary>400: a key, a meter, a tempo or a section of the timeline is not one.</summary>
    public const string BadContent = "bad_content";

    /// <summary>400: a chord's name is not readable (<c>details.chord</c>).</summary>
    public const string UnreadableChord = "unreadable_chord";

    /// <summary>400: a file's name is not a name, or two files have one name (<c>details.name</c>).</summary>
    public const string BadFileName = "bad_file_name";

    /// <summary>400: the content does not name this file, or names it with another content type.</summary>
    public const string FileNotNamed = "file_not_named";

    /// <summary>400: the account it is to be open to does not exist.</summary>
    public const string NoSuchAccount = "no_such_account";

    /// <summary>404: no analysis of this id was put by this client.</summary>
    public const string NoSuchAnalysis = "no_such_analysis";

    /// <summary>400: an analysis of a playlist open to all is open to one person only (<c>details.id</c>).</summary>
    public const string AnalysisNotPublic = "analysis_not_public";

    /// <summary>409: the file was taken away while it was put (it had been dropped, and is named again): put it again.</summary>
    public const string FileReplaced = "file_replaced";

    /// <summary>413: the file is larger than the configuration allows.</summary>
    public const string FileTooLarge = "file_too_large";
}
