using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using PlaylistErrorCodes = HarmonyDB.Shelf.Model.Playlists.PlaylistErrorCodes;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Areas.Playlists.Services;

/// <summary>
/// A playlist as one caller sees it: their membership, and what they may do in it ("as on agile projects": admins
/// remove others' things; adding is for all members or for admins, by the playlist's setting; everyone removes their
/// own; marking, keys and notes are for all members).
/// </summary>
public sealed record PlaylistView(Playlist Playlist, PlaylistMember? Membership)
{
    public bool IsMember => Membership != null;

    public bool IsOwner => Membership?.Role == PlaylistRole.Owner;

    public bool IsAdmin => Membership?.Role is PlaylistRole.Owner or PlaylistRole.Admin;

    public bool CanAdd => IsAdmin || (IsMember && Playlist.AddPolicy == PlaylistAddPolicy.AllMembers);

    /// <summary>
    /// Whether the caller may change or remove a thing added by <paramref name="addedById"/>: their own, or anything
    /// when an admin.
    /// </summary>
    public bool CanChange(Guid? addedById) => IsAdmin || (IsMember && addedById == Membership!.AccountId);

    /// <summary>
    /// Whether the caller may remove a variant of a song: a recording found automatically (no adder) by whoever may add
    /// — removing the wrong goes with adding the missing —, anything else as <see cref="CanChange"/>.
    /// </summary>
    public bool CanRemove(PlaylistSongItem item) => item is { RecordingId: not null, AddedById: null } ? CanAdd : CanChange(item.AddedById);

    public void RequireRemove(PlaylistSongItem item)
    {
        if (item is { RecordingId: not null, AddedById: null })
            RequireAdd();
        else
            RequireChange(item.AddedById);
    }

    public void RequireMember()
    {
        if (!IsMember)
            throw ShelfException.Forbidden(PlaylistErrorCodes.MembersOnly);
    }

    public void RequireAdmin()
    {
        RequireMember();
        if (!IsAdmin)
            throw ShelfException.Forbidden(PlaylistErrorCodes.AdminsOnly);
    }

    public void RequireOwner()
    {
        RequireMember();
        if (!IsOwner)
            throw ShelfException.Forbidden(PlaylistErrorCodes.OwnerOnly);
    }

    public void RequireAdd()
    {
        RequireMember();
        if (!CanAdd)
            throw ShelfException.Forbidden(PlaylistErrorCodes.AddingNotAllowed);
    }

    public void RequireChange(Guid? addedById)
    {
        RequireMember();
        if (!CanChange(addedById))
            throw ShelfException.Forbidden(PlaylistErrorCodes.NotYours);
    }

    public void RequireNotSystem()
    {
        if (Playlist.SystemKind != PlaylistSystemKind.None)
            throw ShelfException.Forbidden(PlaylistErrorCodes.SystemPlaylist);
    }
}

/// <summary>
/// Who sees a playlist and its songs (the concept's "links and guests"):
/// <list type="bullet">
/// <item>its members see it;</item>
/// <item>a <c>public</c> one, everyone, signed in or not;</item>
/// <item>a <c>link</c> one, whoever holds a link to it, or to one of its songs;</item>
/// <item>a song is also seen by whoever holds a link to it: of a <c>private</c> playlist, the song without the
/// playlist.</item>
/// </list>
/// What is not seen answers 404, as if it were not there.
/// </summary>
public sealed class PlaylistGate(ShelfDbContext db)
{
    /// <summary>
    /// The playlist as the caller sees it, for reading.
    /// </summary>
    /// <param name="share">The token of the link the caller opened, if they did.</param>
    public async Task<PlaylistView> ReadAsync(Guid? viewer, Guid playlistId, string? share, CancellationToken cancellationToken)
    {
        var view = await FindAsync(viewer, playlistId, cancellationToken) ?? throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchPlaylist);
        if (!await CanReadAsync(view, share, cancellationToken))
            throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchPlaylist);

        return view;
    }

    /// <summary>
    /// The playlist for a change by a member (the change itself checks the role it needs); not a member who sees it is
    /// answered 403, one who does not, 404.
    /// </summary>
    public async Task<PlaylistView> ForChangeAsync(Guid viewer, Guid playlistId, CancellationToken cancellationToken)
    {
        var view = await ReadAsync(viewer, playlistId, null, cancellationToken);
        view.RequireMember();
        return view;
    }

    /// <summary>
    /// A song of a playlist as the caller sees it, for reading: with the playlist's view, and whether it is seen only
    /// by its link, from a private playlist (then the playlist is not to be shown).
    /// </summary>
    public async Task<(PlaylistView View, PlaylistSong Song, bool FromPrivate)> ReadSongAsync(
        Guid? viewer, Guid playlistId, Guid songId, string? share, CancellationToken cancellationToken)
    {
        var view = await FindAsync(viewer, playlistId, cancellationToken) ?? throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchSong);
        var song = await db.PlaylistSongs.SingleOrDefaultAsync(x => x.Id == songId && x.PlaylistId == playlistId, cancellationToken)
                   ?? throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchSong);

        if (await CanReadAsync(view, share, cancellationToken))
            return (view, song, false);

        var linked = share == null
            ? null
            : await db.ShareLinks.Where(x => x.Token == share && x.Kind == ShareTargetKind.Song).Select(x => x.SongId).SingleOrDefaultAsync(cancellationToken);
        if (linked != null && (await SharedSongAsync(linked.Value, cancellationToken))?.Id == songId)
            return (view, song, true);

        throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchSong);
    }

    /// <summary>
    /// A song of a playlist for a change by a member.
    /// </summary>
    public async Task<(PlaylistView View, PlaylistSong Song)> SongForChangeAsync(Guid viewer, Guid playlistId, Guid songId, CancellationToken cancellationToken)
    {
        var view = await ForChangeAsync(viewer, playlistId, cancellationToken);
        var song = await db.PlaylistSongs.SingleOrDefaultAsync(x => x.Id == songId && x.PlaylistId == playlistId, cancellationToken)
                   ?? throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchSong);
        return (view, song);
    }

    /// <summary>
    /// Whether the caller sees the playlist (with the link they opened, if any).
    /// </summary>
    public async Task<bool> CanReadAsync(PlaylistView view, string? share, CancellationToken cancellationToken)
    {
        if (view.IsMember || view.Playlist.Access == PlaylistAccess.Public)
            return true;

        if (view.Playlist.Access != PlaylistAccess.Link || share == null)
            return false;

        // a song's link opens its playlist too (the song may have been linked into another one there since)
        var playlistId = view.Playlist.Id;
        var songs = db.PlaylistSongs.IgnoreQueryFilters().Where(s => s.PlaylistId == playlistId).Select(s => s.Id);
        return await db.ShareLinks.AnyAsync(x => x.Token == share
                                                 && ((x.Kind == ShareTargetKind.Playlist && x.PlaylistId == playlistId)
                                                     || (x.Kind == ShareTargetKind.Song && songs.Contains(x.SongId!.Value))),
            cancellationToken);
    }

    /// <summary>
    /// The song a song's link leads to now: the song itself, or, when it was linked into another song of its playlist,
    /// the song that took its variants then (so the link keeps working). A song removed on purpose leads nowhere, also
    /// when its chords are put into the playlist again later. Null when it is gone.
    /// </summary>
    public async Task<PlaylistSong?> SharedSongAsync(Guid songId, CancellationToken cancellationToken)
    {
        var song = await db.PlaylistSongs.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == songId, cancellationToken);
        if (song == null || song.DeletedAt == null)
            return song;

        // linking removes the song and its variants at one moment, and the song linked into holds them since then
        var linkedAt = song.DeletedAt;
        var moved = db.PlaylistSongItems.IgnoreQueryFilters().Where(x => x.SongId == songId && x.DeletedAt == linkedAt);
        return await db.PlaylistSongs
            .Where(s => s.PlaylistId == song.PlaylistId
                        && db.PlaylistSongItems.Any(i => i.SongId == s.Id && i.AddedAt <= linkedAt
                                                         && moved.Any(o => (o.ChordsId != null && o.ChordsId == i.ChordsId)
                                                                           || (o.RecordingId != null && o.RecordingId == i.RecordingId))))
            .OrderBy(s => s.Position).ThenBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<PlaylistView?> FindAsync(Guid? viewer, Guid playlistId, CancellationToken cancellationToken)
    {
        var playlist = await db.Playlists.SingleOrDefaultAsync(x => x.Id == playlistId, cancellationToken);
        if (playlist == null)
            return null;

        var membership = viewer == null
            ? null
            : await db.PlaylistMembers.SingleOrDefaultAsync(x => x.PlaylistId == playlistId && x.AccountId == viewer, cancellationToken);
        return new(playlist, membership);
    }
}
