using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Areas.Media.Services;
using HarmonyDB.Shelf.Api.Areas.Music.Services;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Areas.Sync.Services;
using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Api.Common.Items;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Media;
using HarmonyDB.Shelf.Model.Recordings;
using HarmonyDB.Source.Api.Model.V2;
using Microsoft.EntityFrameworkCore;
using M = HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Api.Areas.Recordings.Services;

/// <summary>
/// The recordings of songs in playlists. A song's recordings are its recording variants: when a song gets chords, the
/// best recording found for them on each service is put in by a job (<see cref="SongRecordingsJob"/>; once: one removed
/// is not put back), and the members add the right ones from the candidates — for this playlist only; a recording is
/// removed as any variant is (the playlists area). Each person chooses the one they listen to. The playlist's "search
/// first on" puts that service's recordings first. Who sees and who may do what is the playlists area's
/// (<see cref="PlaylistGate"/>, <see cref="PlaylistView"/>), and so is the adding of songs and variants
/// (<see cref="SongService"/>). Reading a song's recordings only reads.
/// </summary>
public sealed class SongRecordingsService(
    ShelfDbContext db,
    PlaylistGate gate,
    SongService songs,
    UndoTokens undo,
    ChordsReader reader,
    RecordingFinder finder,
    ItemRows rows,
    MusicCatalog music,
    TrackMarks marks,
    MediaService media)
{
    private const int CandidatesShown = 12;

    /// <summary>
    /// The song's recordings, for whoever sees the song (a member, anyone for a public playlist, the holder of a link).
    /// The candidates are those found for the song's chords that are open to the person.
    /// </summary>
    public async Task<SongRecordings> ListAsync(ClaimsPrincipal user, Guid playlistId, Guid songId, string? share, CancellationToken cancellationToken)
    {
        var (view, song, _) = await gate.ReadSongAsync(user.GetAccountId(), playlistId, songId, share, cancellationToken);
        var accountId = user.GetAccountId();
        var searchFirst = view.Playlist.SearchFirst ?? MusicService.Yandex;
        var items = await db.PlaylistSongItems.AsNoTracking()
            .Where(x => x.SongId == song.Id && x.RecordingId != null)
            .Include(x => x.Recording)
            .OrderBy(x => x.Recording!.Service == searchFirst ? 0 : 1)
            .ThenBy(x => x.Position)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var openChords = (await OpenChordsAsync(user, song.Id, cancellationToken)).Select(x => x.Row.Id).ToList();
        var held = items.Select(x => x.RecordingId!.Value).ToHashSet();
        var candidates = await db.ChordsRecordings.AsNoTracking()
            .Where(r => openChords.Contains(r.ChordsId) && !held.Contains(r.RecordingId))
            .OrderBy(r => r.Rank)
            .Select(r => r.Recording)
            .ToListAsync(cancellationToken);

        var choice = accountId == null
            ? null
            : await db.PlaylistSongPreferences.AsNoTracking()
                .Where(x => x.AccountId == accountId && x.SongId == song.Id)
                .Select(x => x.RecordingItemId)
                .SingleOrDefaultAsync(cancellationToken);

        return await marks.ApplyAsync(user, m => new SongRecordings
        {
            SongId = song.Id,
            PlaylistId = song.PlaylistId,
            SearchFirst = MusicShapes.FromDatabase(searchFirst),
            Recordings = items.Select(x => new SongRecording
            {
                ItemId = x.Id,
                Track = m(MusicShapes.Track(x.Recording!)),
                AddedBy = x.AddedById,
                CanRemove = view.CanRemove(x),
            }).ToList(),
            MyChoice = choice is { } chosen && items.Any(x => x.Id == chosen) ? chosen : null,
            Candidates = candidates.DistinctBy(x => x.Id)
                .OrderBy(x => x.Service == searchFirst ? 0 : 1)
                .Take(CandidatesShown)
                .Select(x => m(MusicShapes.Track(x)))
                .ToList(),
            CanAdd = view.CanAdd,
        }, cancellationToken);
    }

    /// <summary>
    /// The address of the sound (or video) of a recording of the song, for whoever sees the song: only a track the
    /// song's recordings read gives (<see cref="ListAsync"/>: one it holds, or one found for its chords open to the
    /// person) is signed; any other is 404 <c>recording_not_of_the_song</c>.
    /// </summary>
    public async Task<MediaAddress> SignTrackAsync(ClaimsPrincipal user, Guid playlistId, Guid songId, string? share, TrackMediaRequest request, CancellationToken cancellationToken)
    {
        var recordings = await ListAsync(user, playlistId, songId, share, cancellationToken);
        if (!recordings.Recordings.Select(x => x.Track).Concat(recordings.Candidates).Any(x => x.Service == request.Service && x.Id == request.Id))
            throw ShelfException.NotFound(RecordingsErrorCodes.NotOfTheSong);

        return media.SignTrack(request);
    }

    /// <summary>
    /// "Find another recording": looks for recordings of the song's chords open to the person (where not looked for
    /// yet), and answers the song's recordings with the candidates.
    /// </summary>
    public async Task<SongRecordings> FindAsync(ClaimsPrincipal user, Guid playlistId, Guid songId, string? share, CancellationToken cancellationToken)
    {
        var (_, song, _) = await gate.ReadSongAsync(user.GetAccountId(), playlistId, songId, share, cancellationToken);
        foreach (var (row, header) in await OpenChordsAsync(user, song.Id, cancellationToken))
            await finder.EnsureFoundAsync(row, header, cancellationToken);

        return await ListAsync(user, playlistId, songId, share, cancellationToken);
    }

    public async Task<M.Changed<M.SongRef>> AddAsync(ClaimsPrincipal user, Guid playlistId, Guid songId, AddRecordingRequest request, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;
        var (view, _) = await gate.SongForChangeAsync(me, playlistId, songId, cancellationToken);
        view.RequireAdd();
        var recording = await RecordingAsync(user, request, cancellationToken);
        return await songs.AddRecordingAsync(me, playlistId, songId, recording.Id, cancellationToken);
    }

    /// <summary>
    /// A new song of the playlist holding one recording and no chords.
    /// </summary>
    public async Task<M.Changed<M.SongRef>> AddSongAsync(ClaimsPrincipal user, Guid playlistId, AddRecordingRequest request, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;
        var view = await gate.ForChangeAsync(me, playlistId, cancellationToken);
        view.RequireAdd();
        var recording = await RecordingAsync(user, request, cancellationToken);
        return await songs.AddRecordingSongAsync(me, playlistId, recording.Id, recording.Title ?? request.TrackId, recording.Artists, cancellationToken);
    }

    /// <summary>
    /// The recording the person listens to in the song (null: none chosen).
    /// </summary>
    public async Task<M.UndoOnly> ChooseAsync(ClaimsPrincipal user, Guid playlistId, Guid songId, string? share, Guid? itemId, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;
        await gate.ReadSongAsync(me, playlistId, songId, share, cancellationToken);
        if (itemId != null && !await db.PlaylistSongItems.AnyAsync(x => x.Id == itemId && x.SongId == songId && x.RecordingId != null, cancellationToken))
            throw ShelfException.NotFound(RecordingsErrorCodes.NoSuchItem);

        var preference = await db.PlaylistSongPreferences.SingleOrDefaultAsync(x => x.AccountId == me && x.SongId == songId, cancellationToken);
        if (preference == null)
        {
            preference = new() { AccountId = me, SongId = songId };
            db.PlaylistSongPreferences.Add(preference);
        }

        preference.RecordingItemId = itemId;
        return new() { Undo = await undo.SaveAsync(me, cancellationToken) };
    }

    /// <summary>
    /// The song's chords that are open to the person, with their rows and the headers the person was given.
    /// </summary>
    private async Task<IReadOnlyList<(ChordsItem Row, Header Header)>> OpenChordsAsync(ClaimsPrincipal user, Guid songId, CancellationToken cancellationToken)
    {
        var chords = await db.PlaylistSongItems
            .Where(x => x.SongId == songId && x.ChordsId != null)
            .OrderBy(x => x.Position)
            .Select(x => x.Chords!)
            .ToListAsync(cancellationToken);
        if (chords.Count == 0)
            return [];

        var answers = await reader.GetSourceHeadersAsync(user, chords.Select(x => x.SourceId).ToList(), cancellationToken);
        return chords.Zip(answers).Where(x => x.Second.State == ItemState.Open).Select(x => (x.First, x.Second.Header!)).ToList();
    }

    /// <summary>
    /// The track's row, described as the service gives it now.
    /// </summary>
    private async Task<Recording> RecordingAsync(ClaimsPrincipal user, AddRecordingRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TrackId))
            throw ShelfException.BadRequest();

        var track = (await music.GetTrackAsync(user, request.Service, request.TrackId, cancellationToken)).Track;
        return await rows.GetOrCreateRecordingAsync(MusicShapes.ToDatabase(request.Service), track.Id, RecordingFinder.Describe(track), cancellationToken);
    }
}

/// <summary>
/// Puts in a song the best recording found for its chords on each service, for the person who gave the song its chords
/// (their account and keys at that moment): for the chords open to them, recordings are looked for (where not looked for
/// yet) by the headers they are given; one the song has or had, or another song of the playlist has, is not put in.
/// </summary>
public sealed class SongRecordingsJob(ShelfDbContext db, IndexClient index, RecordingFinder finder, TimeProvider time) : IJobHandler
{
    public const string Kind = "recordings.find-for-song";

    public sealed record Input(Guid PlaylistId, Guid SongId, IReadOnlyList<string> Keys);

    public async Task<JobCompletion> RunAsync(JobRun run, CancellationToken cancellationToken)
    {
        var input = run.Input<Input>();
        var song = await db.PlaylistSongs.SingleOrDefaultAsync(x => x.Id == input.SongId && x.PlaylistId == input.PlaylistId, cancellationToken);
        if (song == null)
            return new(null);

        var chords = await db.PlaylistSongItems
            .Where(x => x.SongId == song.Id && x.ChordsId != null)
            .Select(x => x.Chords!)
            .ToListAsync(cancellationToken);
        if (chords.Count == 0)
            return new(null);

        var caller = new Caller { Subjects = await AccountSubjects.LoadAsync(db, run.AccountId, cancellationToken), Keys = input.Keys };
        var answers = (await index.Sources.GetHeaders(caller, chords.Select(x => x.SourceId).ToList(), cancellationToken)).Headers;
        var open = chords.Zip(answers).Where(x => x.Second.State == ItemState.Open).ToList();
        foreach (var (row, answer) in open)
            await finder.EnsureFoundAsync(row, answer.Header!, cancellationToken);

        var openIds = open.Select(x => x.First.Id).ToList();
        var best = (await db.ChordsRecordings.AsNoTracking()
                .Where(x => openIds.Contains(x.ChordsId))
                .OrderBy(x => x.Rank)
                .Select(x => new { x.RecordingId, x.Recording.Service })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.Service)
            .Select(x => x.First().RecordingId)
            .ToList();

        var had = await db.PlaylistSongItems.IgnoreQueryFilters()
            .Where(x => x.RecordingId != null
                        && (x.SongId == song.Id || (x.Song.PlaylistId == song.PlaylistId && x.DeletedAt == null && x.Song.DeletedAt == null)))
            .Select(x => x.RecordingId!.Value)
            .ToListAsync(cancellationToken);

        var missing = best.Except(had).ToList();
        if (missing.Count == 0)
            return new(null);

        var position = (await db.PlaylistSongItems.IgnoreQueryFilters().Where(x => x.SongId == song.Id).MaxAsync(x => (double?)x.Position, cancellationToken) ?? 0) + 1;
        var now = time.GetUtcNow();
        foreach (var recordingId in missing)
        {
            db.PlaylistSongItems.Add(new()
            {
                Id = Guid.NewGuid(),
                SongId = song.Id,
                RecordingId = recordingId,
                Position = position++,
                AddedById = null,
                AddedAt = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return new(null);
    }
}
