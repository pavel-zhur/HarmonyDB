using System.ComponentModel;
using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Import.Services;
using HarmonyDB.Shelf.Api.Areas.Mcp.Services;
using HarmonyDB.Shelf.Api.Areas.OAuth.Services;
using HarmonyDB.Shelf.Api.Areas.OwnChords.Services;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Areas.Recordings.Services;
using HarmonyDB.Shelf.Api.Areas.Share.Services;
using HarmonyDB.Shelf.Api.Areas.Sync.Services;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Model.Import;
using HarmonyDB.Shelf.Model.Jobs;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Model.OwnChords;
using HarmonyDB.Shelf.Model.Playlists;
using HarmonyDB.Shelf.Model.Recordings;
using HarmonyDB.Shelf.Model.Share;
using HarmonyDB.Shelf.Model.Sync;

namespace HarmonyDB.Shelf.Api.Areas.Mcp.Tools;

/// <summary>
/// Changing the person's library: playlists, songs, variants, marks, keys, notes, members, invitations, links,
/// recordings, own chords, imports — deleting included. A change answers with what it made (slim) and an undo token
/// where the app's does. A handler's parameters: the services and the caller first, then the tool's arguments — the
/// optional ones with their defaults.
/// </summary>
public static class LibraryChangeTools
{
    private const string Undo = " Returns an undo token (see undo).";

    public static IEnumerable<McpTool> All() =>
    [
        new("create_playlist",
            "Create a playlist owned by this person. access: private (default), link, public. whoMayAdd: allMembers " +
            "(default) or admins." + Undo,
            McpToolEffect.Add,
            async (
                ClaimsPrincipal user, PlaylistService playlists,
                string title,
                [Description("A description.")] string? description = null,
                PlaylistAccess? access = null,
                PlaylistAddPolicy? whoMayAdd = null,
                CancellationToken cancellationToken = default)
                => Playlist(await playlists.CreateAsync(user.Me(), new()
                {
                    Title = title,
                    Description = description,
                    Access = access ?? PlaylistAccess.Private,
                    AddPolicy = whoMayAdd ?? PlaylistAddPolicy.AllMembers,
                }, cancellationToken)),
            BuiltInPermissions.McpLibraryWrite),

        new("update_playlist",
            "Change a playlist's settings (owner or admins); what is not given stays; an empty description clears it." + Undo,
            McpToolEffect.Set,
            async (
                ClaimsPrincipal user, PlaylistService playlists,
                Guid playlistId,
                string? title = null,
                string? description = null,
                PlaylistAccess? access = null,
                PlaylistAddPolicy? whoMayAdd = null,
                CancellationToken cancellationToken = default)
                => Playlist(await playlists.UpdateAsync(user.Me(), playlistId, new() { Title = title, Description = description, Access = access, AddPolicy = whoMayAdd }, cancellationToken)),
            BuiltInPermissions.McpLibraryWrite),

        new("delete_playlist",
            "Delete a playlist (owner only) with its songs, for everyone." + Undo,
            McpToolEffect.Remove,
            (ClaimsPrincipal user, PlaylistService playlists, Guid playlistId, CancellationToken cancellationToken = default)
                => playlists.DeleteAsync(user.Me(), playlistId, cancellationToken),
            BuiltInPermissions.McpLibraryWrite),

        new("leave_playlist",
            "Leave a playlist shared with this person (not the owner)." + Undo,
            McpToolEffect.Remove,
            (ClaimsPrincipal user, PlaylistService playlists, Guid playlistId, CancellationToken cancellationToken = default)
                => playlists.LeaveAsync(user.Me(), playlistId, cancellationToken),
            BuiltInPermissions.McpLibraryWrite),

        new("add_song",
            "Add a song to a playlist: chords by chordsId (title and artists from the chords unless given), or a copy of " +
            "another song this person sees (copyOfSongId). Returns its songId." + Undo,
            McpToolEffect.Add,
            async (
                ClaimsPrincipal user, SongService songs,
                Guid playlistId,
                string? chordsId = null,
                [Description("Instead: a songId to copy.")] Guid? copyOfSongId = null,
                [Description("Title in this playlist.")] string? title = null,
                [Description("Artists in this playlist.")] string[]? artists = null,
                CancellationToken cancellationToken = default)
                => (chordsId == null) == (copyOfSongId == null)
                    ? throw ShelfException.BadRequest()
                    : Song(await songs.AddAsync(user.Me(), playlistId, new() { Chords = chordsId, FromSongId = copyOfSongId, Title = title, Artists = artists }, cancellationToken)),
            BuiltInPermissions.McpLibraryWrite),

        new("update_song",
            "Rename a song, change its artists, or move it (after another song, or to the top)." + Undo,
            McpToolEffect.Set,
            async (
                ClaimsPrincipal user, SongService songs,
                Guid playlistId,
                Guid songId,
                string? title = null,
                string[]? artists = null,
                [Description("Move right after this songId.")] Guid? moveAfterSongId = null,
                [Description("true: move to the top.")] bool? moveToTop = null,
                CancellationToken cancellationToken = default)
                => moveToTop == true && moveAfterSongId != null
                    ? throw ShelfException.BadRequest()
                    : Song(await songs.UpdateAsync(user.Me(), playlistId, songId, new() { Title = title, Artists = artists, After = moveToTop == true ? Guid.Empty : moveAfterSongId }, cancellationToken)),
            BuiltInPermissions.McpLibraryWrite),

        new("remove_song",
            "Remove a song from a playlist." + Undo,
            McpToolEffect.Remove,
            (
                ClaimsPrincipal user, SongService songs,
                Guid playlistId,
                Guid songId,
                CancellationToken cancellationToken = default)
                => songs.RemoveAsync(user.Me(), playlistId, songId, cancellationToken),
            BuiltInPermissions.McpLibraryWrite),

        new("add_variant",
            "Add other chords of the same song to a song, as a variant." + Undo,
            McpToolEffect.Add,
            async (
                ClaimsPrincipal user, SongService songs,
                Guid playlistId,
                Guid songId,
                string chordsId,
                CancellationToken cancellationToken = default)
                => Song(await songs.AddVariantAsync(user.Me(), playlistId, songId, new() { Chords = chordsId }, cancellationToken)),
            BuiltInPermissions.McpLibraryWrite),

        new("remove_variant",
            "Remove a variant (chords or recording) from a song by itemId." + Undo,
            McpToolEffect.Remove,
            (
                ClaimsPrincipal user, SongService songs,
                Guid playlistId,
                Guid songId,
                Guid itemId,
                CancellationToken cancellationToken = default)
                => songs.RemoveVariantAsync(user.Me(), playlistId, songId, itemId, cancellationToken),
            BuiltInPermissions.McpLibraryWrite),

        new("link_songs",
            "Join songs of one playlist that are the same song: the others' variants move into this one and the others are " +
            "removed." + Undo,
            McpToolEffect.Remove,
            async (
                ClaimsPrincipal user, SongService songs,
                Guid playlistId,
                [Description("The songId that stays.")] Guid songId,
                [Description("The songIds that join it.")] Guid[] otherSongIds,
                [Description("The joined song's title.")] string? title = null,
                [Description("Its artists.")] string[]? artists = null,
                CancellationToken cancellationToken = default)
                => Song(await songs.LinkAsync(user.Me(), playlistId, songId, new() { SongIds = otherSongIds ?? [], Title = title, Artists = artists }, cancellationToken)),
            BuiltInPermissions.McpLibraryWrite),

        new("unlink_variant",
            "Take a variant out of a song into a song of its own." + Undo,
            McpToolEffect.Set,
            async (
                ClaimsPrincipal user, SongService songs,
                Guid playlistId,
                Guid songId,
                Guid itemId,
                CancellationToken cancellationToken = default)
                => Song(await songs.UnlinkAsync(user.Me(), playlistId, songId, itemId, cancellationToken)),
            BuiltInPermissions.McpLibraryWrite),

        new("set_main_variant",
            "Choose the chords variant this person sees first in a song; no itemId clears it." + Undo,
            McpToolEffect.Set,
            (
                ClaimsPrincipal user, SongService songs,
                Guid playlistId,
                Guid songId,
                Guid? itemId = null,
                CancellationToken cancellationToken = default)
                => songs.SetMainAsync(user.Me(), playlistId, songId, itemId, null, cancellationToken),
            BuiltInPermissions.McpLibraryWrite),

        new("set_mark",
            "Put a mark on chords, or take it off (on=false). Kinds: " + LibraryTools.MarkKindsText + " (heart_red, " +
            "heart_orange, heart_yellow: how much they like it)." + Undo,
            McpToolEffect.Set,
            (
                ClaimsPrincipal user, SyncService sync, ShelfDbContext db,
                string chordsId,
                [Description("The mark kind.")] string mark,
                [Description("Default true.")] bool? on = null,
                CancellationToken cancellationToken = default)
                => ApplyAsync(user, sync, db, new() { Type = ChangeType.Mark, Chords = chordsId, Kind = mark, On = on ?? true }, cancellationToken),
            BuiltInPermissions.McpLibraryWrite),

        new("set_key",
            "Set this person's key for chords as semitones -11..11, or remove it (no semitones): 'mine' anywhere, or 'here' " +
            "with playlistId." + Undo,
            McpToolEffect.Set,
            (
                ClaimsPrincipal user, SyncService sync, ShelfDbContext db,
                string chordsId,
                [Description("Semitones; none removes.")] int? semitones = null,
                [Description("For the key 'here'.")] Guid? playlistId = null,
                CancellationToken cancellationToken = default)
                => ApplyAsync(user, sync, db, new() { Type = ChangeType.Key, Chords = chordsId, Semitones = semitones, PlaylistId = playlistId }, cancellationToken),
            BuiltInPermissions.McpLibraryWrite),

        new("set_note",
            "Write or remove (empty text) this person's note: personal on chords (chordsId), or on a song (songId), seen by " +
            "its playlist's members." + Undo,
            McpToolEffect.Set,
            (
                ClaimsPrincipal user, SyncService sync, ShelfDbContext db,
                [Description("The note; empty removes.")] string? text = null,
                [Description("For a personal note.")] string? chordsId = null,
                [Description("For a note on a song.")] Guid? songId = null,
                CancellationToken cancellationToken = default)
                => ApplyAsync(user, sync, db, new() { Type = ChangeType.Note, Chords = chordsId, SongId = songId, Text = string.IsNullOrEmpty(text) ? null : text }, cancellationToken),
            BuiltInPermissions.McpLibraryWrite),

        new("create_invitation",
            "Make an invitation token to a playlist (owner or admins): whoever opens it in the app joins as member or admin.",
            McpToolEffect.Add,
            async (
                ClaimsPrincipal user, PlaylistService playlists,
                Guid playlistId,
                [Description("member (default) or admin.")] PlaylistRole? role = null,
                [Description("1-365, default 7.")] int? expiresInDays = null,
                [Description("Uses; unlimited if not given.")] int? maxUses = null,
                CancellationToken cancellationToken = default) =>
            {
                var made = await playlists.CreateInvitationAsync(user.Me(), playlistId, new() { Role = role ?? PlaylistRole.Member, ExpiresInDays = expiresInDays ?? 7, MaxUses = maxUses }, cancellationToken);
                return new { made.Value.Token, made.Value.Role, made.Value.ExpiresAt, made.Undo };
            },
            BuiltInPermissions.McpLibraryWrite),

        new("revoke_invitation",
            "Revoke an invitation by its token." + Undo,
            McpToolEffect.Remove,
            (
                ClaimsPrincipal user, PlaylistService playlists,
                Guid playlistId,
                string token,
                CancellationToken cancellationToken = default)
                => playlists.RevokeInvitationAsync(user.Me(), playlistId, token, cancellationToken),
            BuiltInPermissions.McpLibraryWrite),

        new("set_member_role",
            "Make a playlist member admin or member (owner or admins)." + Undo,
            McpToolEffect.Set,
            async (
                ClaimsPrincipal user, PlaylistService playlists,
                Guid playlistId,
                [Description("The member's accountId.")] Guid accountId,
                PlaylistRole role,
                CancellationToken cancellationToken = default)
                => Playlist(await playlists.SetRoleAsync(user.Me(), playlistId, accountId, role, cancellationToken)),
            BuiltInPermissions.McpLibraryWrite),

        new("remove_member",
            "Remove a member from a playlist (owner or admins)." + Undo,
            McpToolEffect.Remove,
            async (
                ClaimsPrincipal user, PlaylistService playlists,
                Guid playlistId,
                [Description("The member's accountId.")] Guid accountId,
                CancellationToken cancellationToken = default)
                => Playlist(await playlists.RemoveMemberAsync(user.Me(), playlistId, accountId, cancellationToken)),
            BuiltInPermissions.McpLibraryWrite),

        new("create_share_link",
            "Make a link anyone may open without signing in: kind song (playlistId, songId), chords (chordsId), or " +
            "playlist (playlistId, of access 'link'). Returns its url.",
            McpToolEffect.Add,
            async (
                ClaimsPrincipal user, ShareService share, McpAddresses addresses,
                ShareKind kind,
                Guid? playlistId = null,
                Guid? songId = null,
                string? chordsId = null,
                CancellationToken cancellationToken = default) =>
            {
                var link = await share.CreateAsync(user, new() { Kind = kind, PlaylistId = playlistId, SongId = songId, Chords = chordsId }, cancellationToken);
                return new { link.Kind, url = addresses.Url($"/s/{link.Token}") };
            },
            BuiltInPermissions.McpLibraryWrite),

        new("add_recording_to_song",
            "Add a recording (a music service's track) to a song; needs that service's music permission." + Undo,
            McpToolEffect.Add,
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, SongRecordingsService recordings,
                Guid playlistId,
                Guid songId,
                [Description("yandex or youtube.")] MusicService service,
                string trackId,
                CancellationToken cancellationToken = default) =>
            {
                MusicTools.MayRead(user, permissions, service);
                return Song(await recordings.AddAsync(user, playlistId, songId, new AddRecordingRequest { Service = service, TrackId = trackId }, cancellationToken));
            },
            BuiltInPermissions.McpLibraryWrite) { AnyOf = MusicTools.AnyMusic },

        new("choose_recording",
            "Choose the recording of a song this person listens to (itemId); none clears it." + Undo,
            McpToolEffect.Set,
            (
                ClaimsPrincipal user, SongRecordingsService recordings,
                Guid playlistId,
                Guid songId,
                Guid? itemId = null,
                CancellationToken cancellationToken = default)
                => recordings.ChooseAsync(user, playlistId, songId, null, itemId, cancellationToken),
            BuiltInPermissions.McpLibraryWrite),

        new("save_own_chords",
            "Write own chords of this person's: new ones (no ownChordsId; they go to their own-chords playlist, and to " +
            "playlistId+songId as a variant or alsoInPlaylists as new songs), or a new version of theirs (ownChordsId: the " +
            "whole text and fields as they should be; visibility stays unless given). " + IndexTools.SheetFormat +
            " Returns ownChordsId, chordsId, version; new ones also placed (false: saved but put in no playlist, as the " +
            "sources did not answer; chordsId is then null — list_my_own_chords later gives it, to add them to playlists).",
            McpToolEffect.Set,
            async (
                ClaimsPrincipal user, OwnChordsService ownChords,
                [Description("Title.")] string title,
                [Description("Artists, at least one.")] string[] artists,
                [Description("The text with chords.")] string text,
                [Description("To save a new version of these.")] Guid? ownChordsId = null,
                [Description("e.g. 'Am'.")] string? key = null,
                int? bpm = null,
                [Description("Author's comment.")] string? description = null,
                [Description("private (new: default), link or public.")] OwnChordsVisibility? visibility = null,
                [Description("New version: what changed.")] string? versionComment = null,
                [Description("New: playlist of songId.")] Guid? playlistId = null,
                [Description("New: a song to add them to.")] Guid? songId = null,
                [Description("New: also new songs in these playlists.")] Guid[]? alsoInPlaylists = null,
                CancellationToken cancellationToken = default) =>
            {
                if (ownChordsId is not { } id)
                {
                    var created = await ownChords.CreateAsync(user, new()
                    {
                        Text = Text(title, artists, text, key, bpm, description, visibility ?? OwnChordsVisibility.Private, null),
                        PlaylistId = playlistId,
                        SongId = songId,
                        AlsoInPlaylists = alsoInPlaylists,
                    }, cancellationToken);
                    return new { ownChordsId = created.Item.Id, created.Item.ChordsId, created.Item.Version, created.Placed };
                }

                if (playlistId != null || songId != null || alsoInPlaylists != null)
                    throw ShelfException.BadRequest();
                var shown = visibility ?? (await ownChords.GetAsync(user, id, cancellationToken)).Item.Text.Visibility;
                return OwnChords(await ownChords.UpdateAsync(user, id, new() { Text = Text(title, artists, text, key, bpm, description, shown, versionComment) }, cancellationToken));
            },
            BuiltInPermissions.McpLibraryWrite),

        new("delete_own_chords",
            "Delete own chords with every version.",
            McpToolEffect.Remove,
            async (ClaimsPrincipal user, OwnChordsService ownChords, Guid ownChordsId, CancellationToken cancellationToken = default) =>
            {
                await ownChords.DeleteAsync(user, ownChordsId, cancellationToken);
                return new { deleted = true };
            },
            BuiltInPermissions.McpLibraryWrite),

        new("import_chords_links",
            "Import chords pages by their links (up to 100) into the person's imported-chords playlist. A job: see get_job.",
            McpToolEffect.Add,
            async (ClaimsPrincipal user, ImportService import, [Description("The links.")] string[] links, CancellationToken cancellationToken = default)
                => BookTools.Job(await import.StartAsync(user, new ImportLinksRequest { Links = links ?? [] }, cancellationToken)),
            BuiltInPermissions.McpLibraryWrite),

        new("import_music_playlist",
            "Make a shelf playlist from a Yandex Music or YouTube Music playlist link, filled by a job (syncJobId, see " +
            "get_job). Read as the person's linked account with that service's linked permission; with its anonymous " +
            "one only, anonymously (a private playlist then fails: not_found).",
            McpToolEffect.Add,
            async (
                ClaimsPrincipal user, PermissionCatalog permissions, OriginSync origin,
                [Description("The playlist link.")] string url,
                CancellationToken cancellationToken = default) =>
            {
                var found = await origin.ResolveAsync(url, cancellationToken);
                var reading = MusicTools.PlaylistReading(user, permissions, found.Service);
                var imported = await origin.ImportAsync(user.Me(), found, reading, cancellationToken);
                return new { playlist = Slim.Playlist(imported.Value.Playlist), syncJobId = imported.Value.Sync?.Id, reading, imported.Undo };
            },
            BuiltInPermissions.McpLibraryWrite) { AnyOf = MusicTools.AnyMusic },

        new("undo",
            "Take back a change by its undo token, within a few seconds.",
            McpToolEffect.Set,
            async (ClaimsPrincipal user, UndoTokens undo, string token, CancellationToken cancellationToken = default) =>
            {
                await undo.UndoAsync(user.Me(), token, cancellationToken);
                return new { undone = true };
            },
            BuiltInPermissions.McpLibraryWrite),
    ];

    private static object Playlist(Changed<PlaylistDetails> changed) => new { playlist = Slim.Playlist(changed.Value.Playlist), changed.Undo };

    private static object Song<T>(Changed<T> changed)
        where T : SongRef
        => new { songId = changed.Value.Id, changed.Value.PlaylistId, changed.Value.Title, artists = string.Join(", ", changed.Value.Artists), changed.Undo };

    private static object OwnChords(OwnChordsDetails details) => new { ownChordsId = details.Item.Id, details.Item.ChordsId, details.Item.Version };

    /// <summary>
    /// A small change (a mark, a key, a note), made as the app makes it: one change of the sync, answered with its undo.
    /// </summary>
    private static async Task<object> ApplyAsync(ClaimsPrincipal user, SyncService sync, ShelfDbContext db, Change change, CancellationToken cancellationToken)
    {
        // asked since now: the answer carries only this change, not all the person's marks
        var since = await db.GetChangeWatermarkAsync(cancellationToken);
        var result = (await sync.SyncAsync(user, new() { AccountId = user.Me(), Since = since, Changes = [change] }, cancellationToken)).Results.Single();
        if (!result.Applied)
            throw ShelfException.BadRequest(result.Code ?? Model.ErrorCodes.BadRequest);
        return new { applied = true, undo = result.Undo };
    }

    private static OwnChordsText Text(string title, string[] artists, string text, string? key, int? bpm, string? description, OwnChordsVisibility visibility, string? versionComment) => new()
    {
        Title = title,
        Artists = artists ?? [],
        Contents = text,
        Key = key,
        Bpm = bpm,
        Description = description,
        Visibility = visibility,
        VersionComment = versionComment,
    };
}
