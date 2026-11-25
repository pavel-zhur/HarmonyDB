using System.Text.Json;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace HarmonyDB.Shelf.Api.Areas.Mcp.Services;

/// <summary>
/// A tool's failure as the assistant reads it: the shelf's stable code, a sentence it can act on, and the details —
/// a tool result marked as an error, not a protocol error, so that the assistant sees it and can recover.
/// </summary>
public static class McpErrors
{
    /// <summary>
    /// The sentence for each code a tool may meet; a code without one is told by its status.
    /// </summary>
    private static readonly Dictionary<string, string> Sentences = new(StringComparer.Ordinal)
    {
        ["bad_request"] = "A value in the request is not allowed; check the arguments against the tool's description.",
        ["not_found"] = "There is no such thing, or it is not open to this person.",
        ["forbidden"] = "This person may not do that here (it is not theirs to change, or their role does not allow it).",
        ["permission_denied"] = "The person did not allow this assistant to do that; details.permission names what it needs (any one of them).",
        ["conflict"] = "That conflicts with how things are now; read the current state and try again.",
        ["too_many_requests"] = "Too many requests just now; wait a little and try again.",
        ["unavailable"] = "A service this needs does not answer now; try again later.",
        ["internal_error"] = "Something broke on the shelf; try again, and if it repeats, tell the person.",
        ["not_yet"] = "This exists in the app as 'soon' and does not work yet.",
        ["no_such_playlist"] = "No such playlist is open to this person; list_playlists gives the ids.",
        ["no_such_song"] = "No such song in that playlist; list_playlist_songs gives the song ids.",
        ["no_such_variant"] = "No such variant (item) in that song; get_song gives the item ids.",
        ["no_such_item"] = "No such item in that song; get_song gives the item ids.",
        ["no_such_member"] = "That person is not a member of the playlist; get_playlist lists the members.",
        ["no_such_invitation"] = "No such invitation of this playlist; get_playlist lists them.",
        ["no_such_link"] = "No such link.",
        ["no_such_person"] = "No such person.",
        ["no_such_job"] = "No such job of this person's; list_jobs gives the ids.",
        ["no_job_file"] = "The job made no file, or it is no longer kept; make it again.",
        ["no_such_own_chords"] = "No such own chords of this person's; list_my_own_chords gives the ids.",
        ["no_such_service"] = "No such music service.",
        ["chords_not_open"] = "These chords are not open to this person (or not known).",
        ["chords_not_in_playlist"] = "These chords are not in that playlist; a key 'here' is set only on chords of a song of the playlist.",
        ["already_in_playlist"] = "That is already in the playlist.",
        ["already_member"] = "That person is already a member.",
        ["adding_not_allowed"] = "Only the playlist's admins may add songs to it.",
        ["admins_only"] = "Only the playlist's owner and admins may do that.",
        ["owner_only"] = "Only the playlist's owner may do that.",
        ["members_only"] = "Only the playlist's members may do that.",
        ["owner_cannot_leave"] = "The owner cannot leave the playlist; pass the ownership first or delete it.",
        ["system_playlist"] = "This is one of the playlists the app keeps for the person; it cannot be deleted, renamed or left.",
        ["not_yours"] = "It is not this person's to change.",
        ["unknown_mark_kind"] = "No such kind of mark; the tool's description lists the kinds.",
        ["bad_semitones"] = "A key is a number of semitones from -11 to 11.",
        ["too_long"] = "The text is too long.",
        ["stale"] = "A newer change of the same thing came first; read it and decide again.",
        ["nothing_to_undo"] = "There is nothing to undo: the thing was changed again since.",
        ["undo_expired"] = "The undo token expired; make the change back by hand.",
        ["invitation_invalid"] = "The invitation is expired, revoked or used up.",
        ["no_music_link"] = "The person has not linked their account of this music service in the shelf's profile.",
        ["music_link_rejected"] = "The music service refused the person's linked account; they should link it again in the profile.",
        ["not_on_this_service"] = "This is not available on this music service.",
        ["no_lyrics"] = "There are no lyrics for this track.",
        ["no_media"] = "These chords have no recording or notes to play.",
        ["unknown_kind"] = "No such kind of content; the chords' header lists the kinds they have.",
        ["too_many_ids"] = "Too many ids at once.",
        ["too_many_jobs"] = "This person has as many such jobs going as one may; wait for one to end (list_jobs).",
        ["book_bad_items"] = "A book needs from 1 to 500 chords.",
        ["book_bad_item"] = "An item of the book is not valid (a key from -11 to 11).",
        ["import_bad_links"] = "Give from 1 to 100 links.",
        ["link_not_playlist"] = "The link is not a playlist of a music service the shelf knows.",
        ["already_imported"] = "This person already imported that playlist.",
        ["own_chords_title_required"] = "A title is needed.",
        ["own_chords_artists_required"] = "At least one artist is needed.",
        ["own_chords_contents_required"] = "The text with chords is needed.",
        ["own_chords_invalid"] = "A field is too long or out of range.",
        ["song_needs_playlist"] = "A song is given without its playlist, or the other way round.",
        ["not_link_access"] = "A link to a playlist is made only for a playlist whose access is 'link'.",
    };

    public static string Sentence(string code, int statusCode)
        => Sentences.TryGetValue(code, out var sentence)
            ? sentence
            : statusCode switch
            {
                StatusCodes.Status404NotFound => Sentences["not_found"],
                StatusCodes.Status403Forbidden => Sentences["forbidden"],
                StatusCodes.Status409Conflict => Sentences["conflict"],
                StatusCodes.Status429TooManyRequests => Sentences["too_many_requests"],
                StatusCodes.Status501NotImplemented => Sentences["not_yet"],
                >= 500 => Sentences["unavailable"],
                _ => Sentences["bad_request"],
            };

    public static CallToolResult Result(string code, int statusCode, IReadOnlyDictionary<string, string>? details)
    {
        var text = JsonSerializer.Serialize(new { error = code, message = Sentence(code, statusCode), details }, McpJson.Options);
        return new() { IsError = true, Content = [new TextContentBlock { Text = text }] };
    }

    /// <summary>
    /// The tools' filter: a <see cref="ShelfException"/> becomes the error result.
    /// </summary>
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Filter(McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        => async (context, cancellationToken) =>
        {
            try
            {
                return await next(context, cancellationToken);
            }
            catch (ShelfException exception)
            {
                return Result(exception.Code, exception.StatusCode, exception.Details);
            }
            // the arguments could not be bound to the tool's parameters (one required is missing, one is of the wrong
            // type): the binder's message names the argument
            catch (Exception exception) when (exception is ArgumentException { ParamName: "arguments" } or JsonException)
            {
                return Result(Model.ErrorCodes.BadRequest, StatusCodes.Status400BadRequest, new Dictionary<string, string> { ["arguments"] = exception.Message });
            }
        };
}
