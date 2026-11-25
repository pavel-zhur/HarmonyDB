using HarmonyDB.Shelf.Api.Areas.Me.Services;
using HarmonyDB.Shelf.Api.Areas.Sync.Services;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Common.Items;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model;
using Microsoft.EntityFrameworkCore;
using M = HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Api.Areas.Playlists.Services;

/// <summary>
/// A playlist of a music service imported by its link, and its sync by the button — seen and pressed only by the one
/// who imported it, whoever owns the playlist later. The sync is a job (<see cref="PlaylistService.SyncJobKind"/>,
/// done by <see cref="OriginSyncHandler"/>), so leaving the page loses nothing: importing makes the playlist at once,
/// and its first sync names and fills it.
/// </summary>
public sealed class OriginSync(ShelfDbContext db, PlaylistGate gate, PlaylistService playlists, AudioClient audio, JobQueue jobs, UndoTokens undo, TimeProvider time)
{
    public const int MaxUrlLength = 2000;

    public sealed record Input(Guid PlaylistId);

    public async Task<M.Changed<M.PlaylistDetails>> ImportAsync(Guid me, string url, CancellationToken cancellationToken)
    {
        url = url.Trim();
        if (url.Length is 0 or > MaxUrlLength)
            throw ShelfException.BadRequest();

        AudioResolved resolved;
        try
        {
            resolved = await audio.ResolveAsync(url, cancellationToken);
        }
        catch (AudioException exception) when (exception.StatusCode == StatusCodes.Status400BadRequest)
        {
            throw ShelfException.BadRequest(M.PlaylistErrorCodes.LinkNotPlaylist);
        }
        catch (AudioException)
        {
            throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
        }

        if (resolved.Type != "playlist")
            throw ShelfException.BadRequest(M.PlaylistErrorCodes.LinkNotPlaylist);

        var service = resolved.Service == "yandex" ? MusicService.Yandex : MusicService.Youtube;
        var existing = await db.Playlists.Where(x => x.ImportedById == me && x.OriginService == service && x.OriginPlaylistId == resolved.Id)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (existing != null)
            throw ShelfException.Conflict(M.PlaylistErrorCodes.AlreadyImported, new Dictionary<string, string> { ["playlistId"] = existing.Value.ToString() });

        var now = time.GetUtcNow();
        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            OwnerId = me,
            // until the first sync names it
            Title = url.Length > PlaylistService.MaxTitleLength ? url[..PlaylistService.MaxTitleLength] : url,
            Access = PlaylistAccess.Private,
            AddPolicy = PlaylistAddPolicy.AllMembers,
            SearchFirst = service,
            SystemKind = PlaylistSystemKind.None,
            OriginService = service,
            OriginPlaylistId = resolved.Id,
            OriginUrl = url,
            ImportedById = me,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Playlists.Add(playlist);
        db.PlaylistMembers.Add(new() { Id = Guid.NewGuid(), PlaylistId = playlist.Id, AccountId = me, Role = PlaylistRole.Owner, JoinedAt = now });

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var steps = UndoTokens.Capture(db);
        await db.SaveChangesAsync(cancellationToken);
        await jobs.EnqueueAsync(me, PlaylistService.SyncJobKind, playlist.Id.ToString(), new Input(playlist.Id), cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new() { Value = await playlists.GetAsync(me, playlist.Id, null, cancellationToken), Undo = undo.Issue(me, steps) };
    }

    /// <summary>
    /// Starts a sync, unless one is under way.
    /// </summary>
    public async Task<M.PlaylistDetails> StartAsync(Guid me, Guid playlistId, CancellationToken cancellationToken)
    {
        var view = await gate.ForChangeAsync(me, playlistId, cancellationToken);
        if (view.Playlist.OriginService == null || view.Playlist.ImportedById != me)
            throw ShelfException.Forbidden(M.PlaylistErrorCodes.NotImportedByYou);

        if (await jobs.FindActiveAsync(PlaylistService.SyncJobKind, playlistId.ToString(), cancellationToken) == null)
            await jobs.EnqueueAsync(me, PlaylistService.SyncJobKind, playlistId.ToString(), new Input(playlistId), cancellationToken);

        return await playlists.GetAsync(me, playlistId, null, cancellationToken);
    }
}

/// <summary>
/// One sync of an imported playlist: the tracks that are new there are added, as songs with their recording; the songs
/// whose tracks were removed there stay; the songs removed here do not come back (their tracks are remembered); what
/// was changed here stays. Asked as the importer with their own music account when they linked one (a private playlist
/// opens so), otherwise anonymously (the music service reads no playlist by its service account). Run again from the
/// start, it adds nothing twice.
/// </summary>
public sealed class OriginSyncHandler(ShelfDbContext db, AudioClient audio, MusicLinkStore musicLinks, ItemRows itemRows, TimeProvider time, ILogger<OriginSyncHandler> logger)
    : IJobHandler
{
    public async Task<JobCompletion> RunAsync(JobRun run, CancellationToken cancellationToken)
    {
        var playlistId = run.Input<OriginSync.Input>().PlaylistId;
        var playlist = await db.Playlists.SingleOrDefaultAsync(x => x.Id == playlistId, cancellationToken);
        if (playlist?.OriginService == null)
            throw new JobFailedException(M.PlaylistErrorCodes.NoSuchPlaylist);

        var service = playlist.OriginService.Value;
        var importer = playlist.ImportedById ?? run.AccountId;
        AudioPlaylistPage page;
        try
        {
            var acting = await musicLinks.GetActingAsync(importer, Views.Map<Model.Me.MusicService>(service), cancellationToken) ?? AudioActing.Anonymous;
            var answer = await audio.GetPlaylistByLinkAsync(playlist.OriginUrl!, acting, cancellationToken);
            page = (service == MusicService.Yandex ? answer.Data.Yandex : answer.Data.Youtube) ?? throw new JobFailedException(ErrorCodes.Unavailable);
        }
        catch (AudioException exception)
        {
            // the music service's code: credentials_rejected, not_found …
            throw new JobFailedException(exception.Code);
        }
        catch (ShelfException exception)
        {
            throw new JobFailedException(exception.Code);
        }

        var known = (await db.PlaylistSongs.Where(x => x.PlaylistId == playlist.Id && x.OriginTrackId != null).Select(x => x.OriginTrackId!).ToListAsync(cancellationToken))
            .Concat(await db.PlaylistImportTombstones.Where(x => x.PlaylistId == playlist.Id).Select(x => x.OriginTrackId).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var fresh = page.Tracks.Where(x => !string.IsNullOrEmpty(x.Id) && !known.Contains(x.Id!)).DistinctBy(x => x.Id).ToList();
        await run.ReportAsync("adding", 0, cancellationToken);

        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var recordings = new List<(AudioTrack Track, Recording Recording)>();
        foreach (var track in fresh)
        {
            var description = new RecordingDescription(
                track.Title,
                track.Artists.Select(a => a.Name).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a!.Trim()).ToList(),
                track.DurationMs / 1000.0,
                track.Cover);
            recordings.Add((track, await itemRows.GetOrCreateRecordingAsync(service, track.Id!, description, cancellationToken)));
        }

        // a recording already in the playlist (put into another song here) is not added again
        var recordingIds = recordings.Select(x => x.Recording.Id).ToList();
        var present = (await db.PlaylistSongItems
                .Where(x => x.RecordingId != null && recordingIds.Contains(x.RecordingId.Value) && x.Song.PlaylistId == playlist.Id && x.Song.DeletedAt == null)
                .Select(x => x.RecordingId!.Value).ToListAsync(cancellationToken))
            .ToHashSet();

        if (playlist.LastSyncedAt == null && !string.IsNullOrWhiteSpace(page.Playlist.Title))
        {
            // the first sync names it; later the name is the people's here
            playlist.Title = Cut(page.Playlist.Title.Trim());
            if (playlist.Description == null && !string.IsNullOrWhiteSpace(page.Playlist.Description))
                playlist.Description = page.Playlist.Description.Trim();
        }

        var position = (await db.PlaylistSongs.IgnoreQueryFilters().Where(x => x.PlaylistId == playlist.Id).MaxAsync(x => (double?)x.Position, cancellationToken) ?? 0) + 1;
        var added = 0;
        foreach (var (track, recording) in recordings)
        {
            if (!present.Add(recording.Id))
                continue;

            var title = string.IsNullOrWhiteSpace(track.Title) ? track.Id! : track.Title.Trim();
            if (!string.IsNullOrWhiteSpace(track.Version))
                title = $"{title} ({track.Version.Trim()})";

            var song = new PlaylistSong
            {
                Id = Guid.NewGuid(),
                PlaylistId = playlist.Id,
                Title = Cut(title),
                Artists = recording.Artists.ToList(),
                Position = position++,
                AddedById = importer,
                AddedAt = now,
                OriginTrackId = track.Id,
            };
            db.PlaylistSongs.Add(song);
            db.PlaylistSongItems.Add(new() { Id = Guid.NewGuid(), SongId = song.Id, RecordingId = recording.Id, Position = 0, AddedById = importer, AddedAt = now });
            added++;
        }

        playlist.LastSyncedAt = now;
        playlist.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Playlist {PlaylistId} synced: {Added} songs added of {Tracks} tracks.", playlist.Id, added, page.Tracks.Count);
        return new(new { added });
    }

    private static string Cut(string title) => title.Length > PlaylistService.MaxTitleLength ? title[..PlaylistService.MaxTitleLength] : title;
}
