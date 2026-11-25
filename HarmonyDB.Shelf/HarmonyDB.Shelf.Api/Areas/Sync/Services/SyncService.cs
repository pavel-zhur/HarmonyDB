using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Api.Common;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Marks;
using HarmonyDB.Shelf.Model.Sync;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlaylistErrorCodes = HarmonyDB.Shelf.Model.Playlists.PlaylistErrorCodes;

namespace HarmonyDB.Shelf.Api.Areas.Sync.Services;

/// <summary>
/// The small changes the browser makes at once and queues — marks, keys, notes, what was opened — applied one by one
/// in their order, each answered with whether it was applied (or why not) and its undo; and what changed since the
/// browser's watermark: the person's own marks, keys, notes and recent, and the playlists whose content they see
/// changed.
/// <para>
/// The rules: a mark is the person's, on chords, anywhere; a "mine" key and a personal note too. A "here" key and a
/// note in a playlist are for its members, and the key's chords must be in the playlist.
/// </para>
/// </summary>
public sealed class SyncService(ShelfDbContext db, ChordsLookup chordsRows, UndoTokens undo, TimeProvider time)
{
    public const int MaxChanges = 500;
    public const int MaxNoteLength = 10000;

    public async Task<SyncResponse> SyncAsync(ClaimsPrincipal user, SyncRequest request, CancellationToken cancellationToken)
    {
        if (request.Changes.Count > MaxChanges)
            throw ShelfException.BadRequest();

        var me = user.GetAccountId()!.Value;
        var results = new List<ChangeResult>();
        foreach (var change in request.Changes)
        {
            try
            {
                results.Add(await ApplyAsync(user, me, change, cancellationToken));
            }
            // a refused change, or one whose source did not answer: that change fails, the batch goes on
            catch (ShelfException exception) when (exception.StatusCode is >= 400 and < 500 or StatusCodes.Status503ServiceUnavailable)
            {
                db.ChangeTracker.Clear();
                results.Add(new() { Applied = false, Code = exception.Code });
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // the same change made at the same moment elsewhere: it is there
                db.ChangeTracker.Clear();
                results.Add(new() { Applied = true });
            }
        }

        return await ChangesSinceAsync(me, request.Since, results, cancellationToken);
    }

    /// <summary>
    /// A change is made at its moment (<see cref="Change.At"/>, as the browser says: it may have been queued offline);
    /// one made before the thing was last changed or removed is not applied (<c>stale</c>): the answer carries the thing
    /// as it is now, and nothing newer is overwritten by an older change. The rows keep the moments of their changes.
    /// </summary>
    private async Task<ChangeResult> ApplyAsync(ClaimsPrincipal user, Guid me, Change change, CancellationToken cancellationToken)
    {
        var at = Moment(change, time.GetUtcNow());
        switch (change.Type)
        {
            case ChangeType.Mark:
            {
                if (change.Kind == null || !MarkKinds.IsKnown(change.Kind))
                    throw ShelfException.BadRequest(PlaylistErrorCodes.UnknownMarkKind);
                var on = change.On ?? throw ShelfException.BadRequest();
                var chords = await ChordsAsync(user, change, cancellationToken);
                var mark = await db.Marks.SingleOrDefaultAsync(x => x.AccountId == me && x.ChordsId == chords.Id && x.Kind == change.Kind, cancellationToken);
                var removedAt = mark == null
                    ? await db.Marks.IgnoreQueryFilters().Where(x => x.AccountId == me && x.ChordsId == chords.Id && x.Kind == change.Kind).MaxAsync(x => x.DeletedAt, cancellationToken)
                    : null;
                RequireFresh(at, mark?.CreatedAt ?? removedAt);
                if (on && mark == null)
                    db.Marks.Add(new() { Id = Guid.NewGuid(), AccountId = me, ChordsId = chords.Id, Kind = change.Kind, CreatedAt = at });
                else if (!on && mark != null)
                    mark.DeletedAt = at;

                break;
            }

            case ChangeType.Key:
            {
                if (change.Semitones is < -11 or > 11)
                    throw ShelfException.BadRequest(PlaylistErrorCodes.BadSemitones);
                var chords = await ChordsAsync(user, change, cancellationToken);
                if (change.PlaylistId is { } playlistId)
                    await RequireInPlaylistAsync(me, playlistId, chords.Id, cancellationToken);

                var keys = db.Transpositions.Where(x => x.AccountId == me && x.ChordsId == chords.Id && x.PlaylistId == change.PlaylistId);
                var key = await keys.SingleOrDefaultAsync(cancellationToken);
                RequireFresh(at, key?.UpdatedAt ?? (key == null ? await keys.IgnoreQueryFilters().MaxAsync(x => x.DeletedAt, cancellationToken) : null));
                if (change.Semitones is { } semitones)
                {
                    if (key == null)
                        db.Transpositions.Add(new() { Id = Guid.NewGuid(), AccountId = me, ChordsId = chords.Id, PlaylistId = change.PlaylistId, Semitones = semitones, UpdatedAt = at });
                    else if (key.Semitones != semitones)
                    {
                        key.Semitones = semitones;
                        key.UpdatedAt = at;
                    }
                }
                else if (key != null)
                {
                    key.DeletedAt = at;
                }

                break;
            }

            case ChangeType.Note:
            {
                if ((change.Chords == null) == (change.SongId == null))
                    throw ShelfException.BadRequest();
                if (change.Text?.Length > MaxNoteLength)
                    throw ShelfException.BadRequest(PlaylistErrorCodes.TooLong);

                IQueryable<Note> notes;
                Guid? chordsId = null;
                if (change.SongId is { } songId)
                {
                    // a song of one of the person's playlists; any other is no such song, whether it is there or not
                    if (!await db.PlaylistSongs.AnyAsync(x => x.Id == songId && x.Playlist.Members.Any(m => m.AccountId == me && m.DeletedAt == null), cancellationToken))
                        throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchSong);
                    notes = db.Notes.Where(x => x.AccountId == me && x.SongId == songId);
                }
                else
                {
                    chordsId = (await ChordsAsync(user, change, cancellationToken)).Id;
                    notes = db.Notes.Where(x => x.AccountId == me && x.ChordsId == chordsId);
                }

                var note = await notes.SingleOrDefaultAsync(cancellationToken);
                RequireFresh(at, note?.UpdatedAt ?? (note == null ? await notes.IgnoreQueryFilters().MaxAsync(x => x.DeletedAt, cancellationToken) : null));
                if (!string.IsNullOrWhiteSpace(change.Text))
                {
                    if (note == null)
                        db.Notes.Add(new() { Id = Guid.NewGuid(), AccountId = me, ChordsId = chordsId, SongId = change.SongId, Text = change.Text, UpdatedAt = at });
                    else if (note.Text != change.Text)
                    {
                        note.Text = change.Text;
                        note.UpdatedAt = at;
                    }
                }
                else if (note != null)
                {
                    note.DeletedAt = at;
                }

                break;
            }

            case ChangeType.Opened:
            {
                Guid? chordsId = change.Chords == null ? null : (await ChordsAsync(user, change, cancellationToken)).Id;
                if (change.SongId is { } songId)
                {
                    // a song the person may come back to: of their playlists, or a public one
                    var seen = await db.PlaylistSongs.AnyAsync(x => x.Id == songId
                                                                    && (x.Playlist.Access == PlaylistAccess.Public
                                                                        || x.Playlist.Members.Any(m => m.AccountId == me && m.DeletedAt == null)), cancellationToken);
                    if (!seen)
                        throw ShelfException.NotFound(PlaylistErrorCodes.NoSuchSong);
                }
                else if (chordsId == null)
                {
                    throw ShelfException.BadRequest();
                }

                var recent = await db.Recent.SingleOrDefaultAsync(x => x.AccountId == me && x.ChordsId == chordsId && x.SongId == change.SongId, cancellationToken);
                if (recent == null)
                    db.Recent.Add(new() { Id = Guid.NewGuid(), AccountId = me, ChordsId = chordsId, SongId = change.SongId, OpenedAt = at });
                else if (recent.OpenedAt < at)
                    recent.OpenedAt = at;

                // opening is not undone
                await db.SaveChangesAsync(cancellationToken);
                return new() { Applied = true };
            }

            default:
                throw ShelfException.BadRequest();
        }

        return new() { Applied = true, Undo = await undo.SaveAsync(me, cancellationToken) };
    }

    private async Task<SyncResponse> ChangesSinceAsync(Guid me, long? since, List<ChangeResult> results, CancellationToken cancellationToken)
    {
        var watermark = await db.GetChangeWatermarkAsync(cancellationToken);
        // a watermark this server never gave (a database made anew, a browser of long ago): everything
        var all = since == null || since > watermark;
        var from = since ?? 0;

        var marks = await db.Marks.IgnoreQueryFilters()
            .Where(x => x.AccountId == me && (all ? x.DeletedAt == null : x.Version >= from))
            .Select(x => new { x.Chords.SourceId, x.Kind, x.DeletedAt, x.CreatedAt })
            .ToListAsync(cancellationToken);
        var keys = await db.Transpositions.IgnoreQueryFilters()
            .Where(x => x.AccountId == me && (all ? x.DeletedAt == null : x.Version >= from))
            .Select(x => new { x.Chords.SourceId, x.PlaylistId, x.Semitones, x.DeletedAt })
            .ToListAsync(cancellationToken);
        var notes = await db.Notes.IgnoreQueryFilters()
            .Where(x => x.AccountId == me && (all ? x.DeletedAt == null : x.Version >= from))
            .Select(x => new { SourceId = x.Chords == null ? null : x.Chords.SourceId, x.SongId, x.Text, x.DeletedAt, x.UpdatedAt })
            .ToListAsync(cancellationToken);
        var recent = await db.Recent
            .Where(x => x.AccountId == me && (all || x.Version >= from))
            .Select(x => new { SourceId = x.Chords == null ? null : x.Chords.SourceId, x.SongId, x.OpenedAt })
            .ToListAsync(cancellationToken);

        // the playlists whose content the person sees changed: the playlist, its members, songs, variants, others'
        // "here" keys and notes in it, others' marks on its chords
        var playlists = all ? [] : await ChangedPlaylistsAsync(me, from, cancellationToken);

        return new()
        {
            Results = results,
            Watermark = watermark,
            // one answer per thing: the live row over its removed ones
            Marks = marks.GroupBy(x => (x.SourceId, x.Kind))
                .Select(x => x.OrderBy(m => m.DeletedAt == null ? 0 : 1).First())
                .Select(x => new MyMark { Chords = x.SourceId, Kind = x.Kind, On = x.DeletedAt == null, At = x.DeletedAt ?? x.CreatedAt })
                .ToList(),
            Keys = keys.GroupBy(x => (x.SourceId, x.PlaylistId))
                .Select(x => x.OrderBy(k => k.DeletedAt == null ? 0 : 1).First())
                .Select(x => new MyKey { Chords = x.SourceId, PlaylistId = x.PlaylistId, Semitones = x.DeletedAt == null ? x.Semitones : null })
                .ToList(),
            Notes = notes.GroupBy(x => (x.SourceId, x.SongId))
                .Select(x => x.OrderBy(n => n.DeletedAt == null ? 0 : 1).First())
                .Select(x => new MyNote { Chords = x.SourceId, SongId = x.SongId, Text = x.DeletedAt == null ? x.Text : null, UpdatedAt = x.DeletedAt ?? x.UpdatedAt })
                .ToList(),
            Recent = recent.Select(x => new MyRecent { Chords = x.SourceId, SongId = x.SongId, OpenedAt = x.OpenedAt }).ToList(),
            Playlists = playlists,
        };
    }

    private async Task<List<Guid>> ChangedPlaylistsAsync(Guid me, long from, CancellationToken cancellationToken)
    {
        var mine = db.PlaylistMembers.IgnoreQueryFilters().Where(x => x.AccountId == me).Select(x => x.PlaylistId);
        var changed = await db.Playlists.IgnoreQueryFilters()
            .Where(p => mine.Contains(p.Id))
            .Where(p => p.Version >= from
                        || db.PlaylistMembers.IgnoreQueryFilters().Any(x => x.PlaylistId == p.Id && x.Version >= from)
                        || db.PlaylistSongs.IgnoreQueryFilters().Any(x => x.PlaylistId == p.Id && x.Version >= from)
                        || db.PlaylistSongItems.IgnoreQueryFilters().Any(x => x.Song.PlaylistId == p.Id && x.Version >= from)
                        || db.Transpositions.IgnoreQueryFilters().Any(x => x.PlaylistId == p.Id && x.AccountId != me && x.Version >= from)
                        || db.Notes.IgnoreQueryFilters().Any(x => x.Song!.PlaylistId == p.Id && x.AccountId != me && x.Version >= from)
                        || db.Marks.IgnoreQueryFilters().Any(m => m.AccountId != me && m.Version >= from
                                                                   && p.Members.Any(x => x.AccountId == m.AccountId && x.DeletedAt == null)
                                                                   && db.PlaylistSongItems.Any(i => i.ChordsId == m.ChordsId && i.Song.PlaylistId == p.Id)))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        return changed;
    }

    private async Task<ChordsItem> ChordsAsync(ClaimsPrincipal user, Change change, CancellationToken cancellationToken)
        => change.Chords == null
            ? throw ShelfException.BadRequest(ErrorCodes.BadRequest)
            : (await chordsRows.GetOrCreateAsync(user.GetAccountId()!.Value, change.Chords, false, cancellationToken)).Row;

    /// <summary>
    /// A change made before the thing's last change or removal is stale.
    /// </summary>
    private static void RequireFresh(DateTimeOffset at, DateTimeOffset? last)
    {
        if (last != null && at < last)
            throw ShelfException.Conflict(PlaylistErrorCodes.Stale);
    }

    private async Task RequireMemberAsync(Guid me, Guid playlistId, CancellationToken cancellationToken)
    {
        if (!await db.PlaylistMembers.AnyAsync(x => x.PlaylistId == playlistId && x.AccountId == me && x.Playlist.DeletedAt == null, cancellationToken))
            throw ShelfException.Forbidden(PlaylistErrorCodes.MembersOnly);
    }

    private async Task RequireInPlaylistAsync(Guid me, Guid playlistId, Guid chordsId, CancellationToken cancellationToken)
    {
        await RequireMemberAsync(me, playlistId, cancellationToken);
        if (!await db.PlaylistSongItems.AnyAsync(x => x.ChordsId == chordsId && x.Song.PlaylistId == playlistId && x.Song.DeletedAt == null, cancellationToken))
            throw ShelfException.BadRequest(PlaylistErrorCodes.ChordsNotInPlaylist);
    }

    /// <summary>
    /// When the change was made: as the browser says (it may have been offline), never later than now.
    /// </summary>
    private static DateTimeOffset Moment(Change change, DateTimeOffset now) => change.At is { } at && at < now ? at : now;
}
