using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Areas.Recordings.Services;
using HarmonyDB.Shelf.Api.Areas.Sync.Services;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Common;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.EntityFrameworkCore;
using M = HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Api.Areas.Playlists.Services;

/// <summary>
/// The songs of a playlist and their variants: adding (chords, or a copy of a song one sees), renaming, moving,
/// removing; adding and removing a variant; linking songs into one and unlinking a variant into a song of its own;
/// one's main variant.
/// <para>
/// A variant is never moved from one song to another: linking and unlinking mark the variant removed where it was and
/// add it anew where it goes (who added it and when kept), so the letters of both songs follow at once, and the undo
/// brings back the rows themselves. A person's choice of the main variant goes with it.
/// </para>
/// </summary>
public sealed class SongService(
    ShelfDbContext db, PlaylistGate gate, Views views, UndoTokens undo, ChordsLookup chordsRows, JobQueue jobs, SourceCallers callers, TimeProvider time)
{
    public const int MaxArtists = 20;

    public async Task<M.Changed<M.AddedSong>> AddAsync(Guid me, Guid playlistId, M.AddSongRequest request, CancellationToken cancellationToken)
    {
        if ((request.Chords == null) == (request.FromSongId == null))
            throw ShelfException.BadRequest();

        var view = await gate.ForChangeAsync(me, playlistId, cancellationToken);
        view.RequireAdd();
        var now = time.GetUtcNow();
        var position = await NextSongPositionAsync(playlistId, cancellationToken);

        PlaylistSong song;
        var leftOut = 0;
        if (request.Chords != null)
        {
            // only chords open to the person are put into a playlist by them
            var (chords, header) = await chordsRows.GetOrCreateAsync(me, request.Chords, true, cancellationToken);
            await RequireNotInPlaylistAsync(playlistId, [chords.Id], [], cancellationToken);
            // named, unless the person names it, as its source has just told this person
            song = NewSong(playlistId, Title(request.Title ?? header!.Title), Artists(request.Artists ?? header!.Artists), position, me, now);
            db.PlaylistSongItems.Add(NewItem(song.Id, chords.Id, null, 0, me, now));
        }
        else
        {
            var source = await db.PlaylistSongs.SingleOrDefaultAsync(x => x.Id == request.FromSongId, cancellationToken)
                         ?? throw ShelfException.NotFound(M.PlaylistErrorCodes.NoSuchSong);
            await gate.ReadSongAsync(me, source.PlaylistId, source.Id, request.Share, cancellationToken);

            // a copy takes the variants open to the person (and the recordings); one left with none is not made
            var items = await db.PlaylistSongItems.Where(x => x.SongId == source.Id).Include(x => x.Chords)
                .OrderBy(x => x.Position).ThenBy(x => x.Id).ToListAsync(cancellationToken);
            var open = await chordsRows.OpenAsync(me, items.Where(x => x.Chords != null).Select(x => x.Chords!.SourceId).ToList(), cancellationToken);
            var taken = items.Where(x => x.Chords == null || open.Contains(x.Chords.SourceId)).ToList();
            leftOut = items.Count - taken.Count;
            if (taken.Count == 0)
                throw new ShelfException(StatusCodes.Status403Forbidden, M.PlaylistErrorCodes.ChordsNotOpen, new Dictionary<string, string> { ["leftOut"] = leftOut.ToString() });

            await RequireNotInPlaylistAsync(playlistId,
                taken.Where(x => x.ChordsId != null).Select(x => x.ChordsId!.Value).ToList(),
                taken.Where(x => x.RecordingId != null).Select(x => x.RecordingId!.Value).ToList(),
                cancellationToken);
            song = NewSong(playlistId, source.Title, source.Artists.ToList(), position, me, now);
            var i = 0;
            foreach (var item in taken)
                db.PlaylistSongItems.Add(NewItem(song.Id, item.ChordsId, item.RecordingId, i++, me, now));
        }

        db.PlaylistSongs.Add(song);
        if (db.PlaylistSongItems.Local.Any(x => x.SongId == song.Id && x.ChordsId != null))
            await FindRecordingsAsync(me, playlistId, song.Id, cancellationToken);
        var token = await undo.SaveAsync(me, cancellationToken);
        var added = Views.Song(song, await views.PeopleAsync([me], cancellationToken));
        return new() { Value = new M.AddedSong(added) { LeftOut = leftOut }, Undo = token };
    }

    /// <summary>
    /// Renames or moves a song: its adder, or an admin.
    /// </summary>
    public async Task<M.Changed<M.SongRef>> UpdateAsync(Guid me, Guid playlistId, Guid songId, M.UpdateSongRequest request, CancellationToken cancellationToken)
    {
        var (view, song) = await gate.SongForChangeAsync(me, playlistId, songId, cancellationToken);
        view.RequireChange(song.AddedById);
        if (request.Title != null)
            song.Title = Title(request.Title);
        if (request.Artists != null)
            song.Artists = Artists(request.Artists);
        if (request.After is { } after)
            song.Position = await PositionAfterAsync(playlistId, songId, after, cancellationToken);

        var token = await undo.SaveAsync(me, cancellationToken);
        return new() { Value = Views.Song(song, await views.PeopleAsync(song.AddedById is { } by ? [by] : [], cancellationToken)), Undo = token };
    }

    /// <summary>
    /// Removes a song: its adder, or an admin. A song of an imported playlist does not come back with the next sync.
    /// </summary>
    public async Task<M.UndoOnly> RemoveAsync(Guid me, Guid playlistId, Guid songId, CancellationToken cancellationToken)
    {
        var (view, song) = await gate.SongForChangeAsync(me, playlistId, songId, cancellationToken);
        view.RequireChange(song.AddedById);
        await RemoveSongAsync(song, time.GetUtcNow(), cancellationToken);
        return new() { Undo = await undo.SaveAsync(me, cancellationToken) };
    }

    public async Task<M.Changed<M.SongRef>> AddVariantAsync(Guid me, Guid playlistId, Guid songId, M.AddVariantRequest request, CancellationToken cancellationToken)
    {
        var (view, song) = await gate.SongForChangeAsync(me, playlistId, songId, cancellationToken);
        view.RequireAdd();
        var (chords, _) = await chordsRows.GetOrCreateAsync(me, request.Chords, true, cancellationToken);
        await RequireNotInPlaylistAsync(playlistId, [chords.Id], [], cancellationToken);
        db.PlaylistSongItems.Add(NewItem(songId, chords.Id, null, await NextItemPositionAsync(songId, cancellationToken), me, time.GetUtcNow()));
        await FindRecordingsAsync(me, playlistId, songId, cancellationToken);
        var token = await undo.SaveAsync(me, cancellationToken);
        return new() { Value = Views.Song(song, await views.PeopleAsync(song.AddedById is { } by ? [by] : [], cancellationToken)), Undo = token };
    }

    /// <summary>
    /// Adds a recording (a music service's track, its local row) to a song, as one of its variants.
    /// </summary>
    public async Task<M.Changed<M.SongRef>> AddRecordingAsync(Guid me, Guid playlistId, Guid songId, Guid recordingId, CancellationToken cancellationToken)
    {
        var (view, song) = await gate.SongForChangeAsync(me, playlistId, songId, cancellationToken);
        view.RequireAdd();
        await RequireNotInPlaylistAsync(playlistId, [], [recordingId], cancellationToken);
        db.PlaylistSongItems.Add(NewItem(songId, null, recordingId, await NextItemPositionAsync(songId, cancellationToken), me, time.GetUtcNow()));
        var token = await undo.SaveAsync(me, cancellationToken);
        return new() { Value = Views.Song(song, await views.PeopleAsync(song.AddedById is { } by ? [by] : [], cancellationToken)), Undo = token };
    }

    /// <summary>
    /// Adds a song that is a recording alone (a music service's track, its local row), with no chords.
    /// </summary>
    public async Task<M.Changed<M.SongRef>> AddRecordingSongAsync(
        Guid me, Guid playlistId, Guid recordingId, string title, IReadOnlyList<string> artists, CancellationToken cancellationToken)
    {
        var view = await gate.ForChangeAsync(me, playlistId, cancellationToken);
        view.RequireAdd();
        await RequireNotInPlaylistAsync(playlistId, [], [recordingId], cancellationToken);
        var now = time.GetUtcNow();
        var song = NewSong(playlistId, Title(title), Artists(artists), await NextSongPositionAsync(playlistId, cancellationToken), me, now);
        db.PlaylistSongs.Add(song);
        db.PlaylistSongItems.Add(NewItem(song.Id, null, recordingId, 0, me, now));
        var token = await undo.SaveAsync(me, cancellationToken);
        return new() { Value = Views.Song(song, await views.PeopleAsync([me], cancellationToken)), Undo = token };
    }

    /// <summary>
    /// Removes a variant: its adder, or an admin. The letters after it shift. A song left with nothing of its own
    /// (<see cref="Keeps"/>) is removed with it.
    /// </summary>
    public async Task<M.UndoOnly> RemoveVariantAsync(Guid me, Guid playlistId, Guid songId, Guid itemId, CancellationToken cancellationToken)
    {
        var (view, song) = await gate.SongForChangeAsync(me, playlistId, songId, cancellationToken);
        var items = await LiveItemsAsync(songId, cancellationToken);
        var item = items.SingleOrDefault(x => x.Id == itemId) ?? throw ShelfException.NotFound(M.PlaylistErrorCodes.NoSuchVariant);
        view.RequireRemove(item);

        var now = time.GetUtcNow();
        item.DeletedAt = now;
        if (!Keeps(items.Where(x => x.Id != itemId)))
            await RemoveSongAsync(song, now, cancellationToken);

        return new() { Undo = await undo.SaveAsync(me, cancellationToken) };
    }

    /// <summary>
    /// Links other songs of the playlist into this one: their variants join it (after its own, in their order), and they
    /// are removed. Each person's main variant of a song linked in becomes their main one here, unless they have one
    /// here. The notes written on them come here too (one person's two notes joined into one).
    /// </summary>
    public async Task<M.Changed<M.SongRef>> LinkAsync(Guid me, Guid playlistId, Guid songId, M.LinkSongsRequest request, CancellationToken cancellationToken)
    {
        var otherIds = request.SongIds.Distinct().ToList();
        if (otherIds.Count == 0 || otherIds.Contains(songId))
            throw ShelfException.BadRequest();

        var (view, song) = await gate.SongForChangeAsync(me, playlistId, songId, cancellationToken);
        view.RequireChange(song.AddedById);
        var others = await db.PlaylistSongs.Where(x => otherIds.Contains(x.Id) && x.PlaylistId == playlistId).ToListAsync(cancellationToken);
        if (others.Count != otherIds.Count)
            throw ShelfException.NotFound(M.PlaylistErrorCodes.NoSuchSong);
        foreach (var other in others)
            view.RequireChange(other.AddedById);

        // the name of the song they make, asked while linking
        if (request.Title != null)
            song.Title = Title(request.Title);
        if (request.Artists != null)
            song.Artists = Artists(request.Artists);

        var now = time.GetUtcNow();
        var items = await LiveItemsAsync(songId, cancellationToken);
        var position = items.Count == 0 ? 0 : items.Max(x => x.Position) + 1;
        var moved = new Dictionary<Guid, Guid>();
        foreach (var other in others.OrderBy(x => otherIds.IndexOf(x.Id)))
        {
            foreach (var item in await LiveItemsAsync(other.Id, cancellationToken))
            {
                item.DeletedAt = now;
                var present = items.FirstOrDefault(x => (item.ChordsId != null && x.ChordsId == item.ChordsId) || (item.RecordingId != null && x.RecordingId == item.RecordingId));
                if (present != null)
                {
                    moved[item.Id] = present.Id;
                    continue;
                }

                var copy = NewItem(songId, item.ChordsId, item.RecordingId, position++, item.AddedById, item.AddedAt);
                db.PlaylistSongItems.Add(copy);
                items.Add(copy);
                moved[item.Id] = copy.Id;
            }

            await RemoveSongAsync(other, now, cancellationToken);
        }

        await CarryPreferencesAsync(otherIds, songId, moved, cancellationToken);
        await CarryNotesAsync(otherIds, songId, now, cancellationToken);
        var token = await undo.SaveAsync(me, cancellationToken);
        return new() { Value = Views.Song(song, await views.PeopleAsync(song.AddedById is { } by ? [by] : [], cancellationToken)), Undo = token };
    }

    /// <summary>
    /// Takes a variant out into a song of its own, right after this one, named as its chords are (or as this song,
    /// when they have no name yet). Whoever had it as the main variant has it as the main one of the new song.
    /// </summary>
    public async Task<M.Changed<M.SongRef>> UnlinkAsync(Guid me, Guid playlistId, Guid songId, Guid itemId, CancellationToken cancellationToken)
    {
        var (view, song) = await gate.SongForChangeAsync(me, playlistId, songId, cancellationToken);
        view.RequireChange(song.AddedById);
        var items = await LiveItemsAsync(songId, cancellationToken);
        var item = items.SingleOrDefault(x => x.Id == itemId) ?? throw ShelfException.NotFound(M.PlaylistErrorCodes.NoSuchVariant);
        if (item.ChordsId == null || !Keeps(items.Where(x => x.Id != itemId)))
            throw ShelfException.BadRequest();

        // named as its source tells the person unlinking; when not open to them, as this song
        var sourceId = await db.Chords.Where(x => x.Id == item.ChordsId).Select(x => x.SourceId).SingleAsync(cancellationToken);
        var header = await chordsRows.HeaderAsync(me, sourceId, cancellationToken);
        var now = time.GetUtcNow();
        var created = NewSong(
            playlistId,
            header?.Title ?? song.Title,
            header != null ? header.Artists.ToList() : song.Artists.ToList(),
            await PositionAfterAsync(playlistId, null, songId, cancellationToken),
            me,
            now);
        db.PlaylistSongs.Add(created);
        item.DeletedAt = now;
        var copy = NewItem(created.Id, item.ChordsId, null, 0, item.AddedById, item.AddedAt);
        db.PlaylistSongItems.Add(copy);
        await CarryPreferencesAsync([songId], created.Id, new() { [item.Id] = copy.Id }, cancellationToken);
        var token = await undo.SaveAsync(me, cancellationToken);
        return new() { Value = Views.Song(created, await views.PeopleAsync([me], cancellationToken)), Undo = token };
    }

    /// <summary>
    /// The person's main variant of the song (null: none chosen): what opens when they tap the song.
    /// </summary>
    public async Task<M.UndoOnly> SetMainAsync(Guid me, Guid playlistId, Guid songId, Guid? itemId, string? share, CancellationToken cancellationToken)
    {
        await gate.ReadSongAsync(me, playlistId, songId, share, cancellationToken);
        if (itemId != null && !await db.PlaylistSongItems.AnyAsync(x => x.Id == itemId && x.SongId == songId && x.ChordsId != null, cancellationToken))
            throw ShelfException.NotFound(M.PlaylistErrorCodes.NoSuchVariant);

        var preference = await db.PlaylistSongPreferences.SingleOrDefaultAsync(x => x.AccountId == me && x.SongId == songId, cancellationToken);
        if (preference == null)
        {
            preference = new() { AccountId = me, SongId = songId };
            db.PlaylistSongPreferences.Add(preference);
        }

        preference.MainItemId = itemId;
        return new() { Undo = await undo.SaveAsync(me, cancellationToken) };
    }

    private async Task CarryPreferencesAsync(IReadOnlyList<Guid> fromSongs, Guid toSong, Dictionary<Guid, Guid> moved, CancellationToken cancellationToken)
    {
        var from = await db.PlaylistSongPreferences.IgnoreQueryFilters()
            .Where(x => fromSongs.Contains(x.SongId) && x.MainItemId != null && moved.Keys.Contains(x.MainItemId.Value))
            .ToListAsync(cancellationToken);
        var accounts = from.Select(x => x.AccountId).Distinct().ToList();
        var here = await db.PlaylistSongPreferences.Where(x => x.SongId == toSong && accounts.Contains(x.AccountId)).ToDictionaryAsync(x => x.AccountId, cancellationToken);
        foreach (var preference in from.DistinctBy(x => x.AccountId))
        {
            if (!here.TryGetValue(preference.AccountId, out var target))
            {
                target = new() { AccountId = preference.AccountId, SongId = toSong };
                db.PlaylistSongPreferences.Add(target);
                here[preference.AccountId] = target;
            }

            // a choice made here stays, unless it points at a variant no longer here
            if (target.MainItemId == null || moved.ContainsKey(target.MainItemId.Value))
                target.MainItemId = moved[preference.MainItemId!.Value];
        }
    }

    private async Task CarryNotesAsync(IReadOnlyList<Guid> fromSongs, Guid toSong, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var notes = await db.Notes.Where(x => x.SongId != null && fromSongs.Contains(x.SongId.Value)).OrderBy(x => x.UpdatedAt).ToListAsync(cancellationToken);
        if (notes.Count == 0)
            return;

        var accounts = notes.Select(x => x.AccountId).Distinct().ToList();
        var here = await db.Notes.Where(x => x.SongId == toSong && accounts.Contains(x.AccountId)).ToDictionaryAsync(x => x.AccountId, cancellationToken);
        foreach (var note in notes)
        {
            if (here.TryGetValue(note.AccountId, out var target))
            {
                target.Text = $"{target.Text}\n\n{note.Text}";
                target.UpdatedAt = now;
                note.DeletedAt = now;
            }
            else
            {
                note.SongId = toSong;
                here[note.AccountId] = note;
            }
        }
    }

    /// <summary>
    /// Marks the song removed; of an imported playlist, its track is remembered so that the sync does not bring it back.
    /// </summary>
    public async Task RemoveSongAsync(PlaylistSong song, DateTimeOffset now, CancellationToken cancellationToken)
    {
        song.DeletedAt = now;
        if (song.OriginTrackId != null
            && !db.PlaylistImportTombstones.Local.Any(x => x.PlaylistId == song.PlaylistId && x.OriginTrackId == song.OriginTrackId)
            && !await db.PlaylistImportTombstones.AnyAsync(x => x.PlaylistId == song.PlaylistId && x.OriginTrackId == song.OriginTrackId, cancellationToken))
            db.PlaylistImportTombstones.Add(new() { PlaylistId = song.PlaylistId, OriginTrackId = song.OriginTrackId, RemovedAt = now });
    }

    /// <summary>
    /// Whether a song with these variants left stays: it has chords, or a recording someone put in on purpose. One left
    /// with only recordings found for it automatically (no adder) goes.
    /// </summary>
    public static bool Keeps(IEnumerable<PlaylistSongItem> remaining) => remaining.Any(x => x.ChordsId != null || x.AddedById != null);

    /// <summary>
    /// Queues, with the act about to be saved, the finding of recordings for a song that got chords: a follow-up of the
    /// shelf's, done with the adder's account and keys (the job of the recordings' area; queued here directly, as that
    /// area's service stands on this one). One already waiting for the song does it.
    /// </summary>
    private async Task FindRecordingsAsync(Guid me, Guid playlistId, Guid songId, CancellationToken cancellationToken)
    {
        var caller = callers.For(await chordsRows.PersonAsync(me, cancellationToken));
        await jobs.QueueFollowUp(me, SongRecordingsJob.Kind, songId.ToString(), new SongRecordingsJob.Input(playlistId, songId, caller.Keys), cancellationToken);
    }

    private async Task RequireNotInPlaylistAsync(Guid playlistId, IReadOnlyList<Guid> chordsIds, IReadOnlyList<Guid> recordingIds, CancellationToken cancellationToken)
    {
        var present = await db.PlaylistSongItems
            .Where(x => x.Song.PlaylistId == playlistId && x.Song.DeletedAt == null
                        && ((x.ChordsId != null && chordsIds.Contains(x.ChordsId.Value)) || (x.RecordingId != null && recordingIds.Contains(x.RecordingId.Value))))
            .Select(x => (Guid?)x.SongId).FirstOrDefaultAsync(cancellationToken);
        if (present != null)
            throw ShelfException.Conflict(M.PlaylistErrorCodes.AlreadyInPlaylist, new Dictionary<string, string> { ["songId"] = present.Value.ToString() });
    }

    /// <summary>
    /// The song's variants not removed, in their order.
    /// </summary>
    private Task<List<PlaylistSongItem>> LiveItemsAsync(Guid songId, CancellationToken cancellationToken)
        => db.PlaylistSongItems.Where(x => x.SongId == songId).OrderBy(x => x.Position).ThenBy(x => x.Id).ToListAsync(cancellationToken);

    private async Task<double> NextSongPositionAsync(Guid playlistId, CancellationToken cancellationToken)
        => (await db.PlaylistSongs.IgnoreQueryFilters().Where(x => x.PlaylistId == playlistId).MaxAsync(x => (double?)x.Position, cancellationToken) ?? 0) + 1;

    private async Task<double> NextItemPositionAsync(Guid songId, CancellationToken cancellationToken)
        => (await db.PlaylistSongItems.Where(x => x.SongId == songId).MaxAsync(x => (double?)x.Position, cancellationToken) ?? -1) + 1;

    /// <summary>
    /// A position right after the song <paramref name="after"/> (<see cref="Guid.Empty"/>: before all), between it and
    /// the next one (<paramref name="moving"/> left out).
    /// </summary>
    private async Task<double> PositionAfterAsync(Guid playlistId, Guid? moving, Guid after, CancellationToken cancellationToken)
    {
        var songs = db.PlaylistSongs.Where(x => x.PlaylistId == playlistId && x.Id != moving);
        if (after == Guid.Empty)
            return (await songs.MinAsync(x => (double?)x.Position, cancellationToken) ?? 1) - 1;

        var anchor = await songs.Where(x => x.Id == after).Select(x => (double?)x.Position).SingleOrDefaultAsync(cancellationToken)
                     ?? throw ShelfException.NotFound(M.PlaylistErrorCodes.NoSuchSong);
        var next = await songs.Where(x => x.Position > anchor).MinAsync(x => (double?)x.Position, cancellationToken);
        return next == null ? anchor + 1 : (anchor + next.Value) / 2;
    }

    private static PlaylistSong NewSong(Guid playlistId, string title, List<string> artists, double position, Guid? by, DateTimeOffset at)
        => new() { Id = Guid.NewGuid(), PlaylistId = playlistId, Title = title, Artists = artists, Position = position, AddedById = by, AddedAt = at };

    private static PlaylistSongItem NewItem(Guid songId, Guid? chordsId, Guid? recordingId, double position, Guid? by, DateTimeOffset at)
        => new() { Id = Guid.NewGuid(), SongId = songId, ChordsId = chordsId, RecordingId = recordingId, Position = position, AddedById = by, AddedAt = at };

    public static string Title(string? title) => PlaylistService.Title(title);

    public static List<string> Artists(IReadOnlyList<string> artists)
    {
        var list = artists.Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        if (list.Count > MaxArtists || list.Any(x => x.Length > PlaylistService.MaxTitleLength))
            throw ShelfException.BadRequest(M.PlaylistErrorCodes.TooLong);

        return list;
    }
}
