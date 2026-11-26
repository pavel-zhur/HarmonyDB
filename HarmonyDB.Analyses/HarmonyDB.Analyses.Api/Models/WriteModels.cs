using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Contents;

namespace HarmonyDB.Analyses.Api.Models;

/// <summary>
/// An analysis of one recording, put by an internal client, written with the source contract's JSON options. Put again
/// with the same <see cref="Key"/> by the same client, it replaces what was put before, under the same id.
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
    /// Who ordered it, as an opaque string, when there is someone to name. Kept; it decides nothing and is never given
    /// out.
    /// </summary>
    public string? OrderedBy { get; init; }

    /// <summary>
    /// The track of a music service it was made from, when it was made from one (not when from an uploaded file).
    /// </summary>
    public MusicServiceTrack? MadeFrom { get; init; }

    /// <summary>
    /// Its content in the source contract's kinds, each kept and given as it is put. The timeline is required; the sheet,
    /// when there is one, is to be one that can be read and drawn. A file of the analysis (<see cref="FileRef"/>: the
    /// recording, a separate instrument, a MIDI file) is named by its <see cref="FileRef.Name"/> and put next, in the
    /// content type given here.
    /// </summary>
    public required Content Content { get; init; }
}

public sealed record PutAnalysisResponse
{
    /// <summary>
    /// Its id as the source's item.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The files its content names, to be put.
    /// </summary>
    public required IReadOnlyList<string> Files { get; init; }
}

/// <summary>
/// A file of an analysis, as it is kept.
/// </summary>
public sealed record AnalysisFileInfo
{
    public required string Name { get; init; }

    public required string ContentType { get; init; }

    public required long Bytes { get; init; }

    /// <summary>
    /// The SHA-256 of its bytes, lower-case hex.
    /// </summary>
    public required string Sha256 { get; init; }
}

/// <summary>
/// An error of the write side: <c>{ "code": …, "name": … }</c> with its status.
/// </summary>
public sealed record AnalysesError
{
    public required string Code { get; init; }

    /// <summary>
    /// The file the error is about, when it is about one.
    /// </summary>
    public string? Name { get; init; }
}

/// <summary>
/// The error codes of the write side.
/// </summary>
public static class AnalysesErrorCodes
{
    /// <summary>400: the request is not whole (a key, a title, artists, the content).</summary>
    public const string BadRequest = "bad_request";

    /// <summary>400: the content has no timeline.</summary>
    public const string TimelineMissing = "timeline_missing";

    /// <summary>400: the sheet is not one that can be read and drawn.</summary>
    public const string BadSheet = "bad_sheet";

    /// <summary>400: a sound is both a track and a file, or neither.</summary>
    public const string BadAudio = "bad_audio";

    /// <summary>400: a file's name is not a name, has no content type, or two files have one name.</summary>
    public const string BadFileName = "bad_file_name";

    /// <summary>400: the content does not name this file, or names it with another content type.</summary>
    public const string FileNotNamed = "file_not_named";

    /// <summary>404: no analysis of this id was put by this client.</summary>
    public const string NoSuchAnalysis = "no_such_analysis";

    /// <summary>413: the file is larger than the settings allow.</summary>
    public const string FileTooLarge = "file_too_large";

    /// <summary>409: the content stopped naming the file while it was put: put the content again first.</summary>
    public const string FileDropped = "file_dropped";
}

/// <summary>
/// An error of the write side, answered as <see cref="AnalysesError"/> with its status.
/// </summary>
public sealed class AnalysesException(int statusCode, string code, string? name = null) : Exception(code)
{
    public int StatusCode { get; } = statusCode;

    public AnalysesError Error { get; } = new() { Code = code, Name = name };
}
