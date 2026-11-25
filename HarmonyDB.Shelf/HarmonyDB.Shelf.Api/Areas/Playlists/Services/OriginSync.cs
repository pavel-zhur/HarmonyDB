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
/// How a sync may read the playlist of the music service: as its importer, with their own music account when they linked
/// one (a private playlist opens so), or anonymously (only what anyone may read). It is the right of the one who started
/// the sync, and goes in the sync's input at its start: a run repeated after an interruption reads as the first did.
/// </summary>
public enum OriginReading
{
    Anonymous,
    AsImporter,
}

/// <summary>
/// A playlist of a music service imported by its link or by its service and id, and its sync by the button — seen and
/// pressed only by the one who imported it, whoever owns the playlist later. The sync is a job
/// (<see cref="PlaylistService.SyncJobKind"/>, done by <see cref="OriginSyncHandler"/>), so leaving the page loses
/// nothing: importing makes the playlist at once, and its first sync names and fills it, read as the import allowed
/// (<see cref="OriginReading"/>). The person's own session reads as the importer; a client acting for them says how it
/// may read.
/// </summary>
public sealed class OriginSync(ShelfDbContext db, PlaylistGate gate, PlaylistService playlists, AudioClient audio, JobQueue jobs, UndoTokens undo, TimeProvider time)
{
    public const int MaxUrlLength = 2000;

    public const int MaxPlaylistIdLength = 200;

    /// <param name="Reading">How this sync may read the music service; a sync queued before it was said reads anonymously.</param>
    public sealed record Input(Guid PlaylistId, OriginReading Reading);

    /// <summary>
    /// A playlist of a music service, as a link or a service and an id name it.
    /// </summary>
    /// <param name="Url">The link it was named by, if it was.</param>
    public sealed record Origin(Model.Me.MusicService Service, string PlaylistId, string? Url);

    /// <summary>
    /// The person's own import (the app's): by a link, or by a service and an id — one of the two, whole; read as the
    /// importer.
    /// </summary>
    public async Task<M.Changed<M.PlaylistDetails>> ImportAsync(Guid me, M.ImportPlaylistRequest request, CancellationToken cancellationToken)
    {
        var origin = request switch
        {
            { Url: { } url, Service: null, PlaylistId: null } => await ResolveAsync(url, cancellationToken),
            { Url: null, Service: { } service, PlaylistId: { } id } => ById(service, id),
            _ => throw ShelfException.BadRequest(),
        };

        return await ImportAsync(me, origin, OriginReading.AsImporter, cancellationToken);
    }

    /// <summary>
    /// A playlist by its service and id. Nothing is asked of the music service.
    /// </summary>
    public static Origin ById(Model.Me.MusicService service, string playlistId)
    {
        playlistId = playlistId.Trim();
        if (playlistId.Length is 0 or > MaxPlaylistIdLength)
            throw ShelfException.BadRequest();

        return new(service, playlistId, null);
    }

    /// <summary>
    /// What playlist a link names (the music service reads the link, not the playlist).
    /// </summary>
    public async Task<Origin> ResolveAsync(string url, CancellationToken cancellationToken)
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

        return new(resolved.Service == "yandex" ? Model.Me.MusicService.Yandex : Model.Me.MusicService.Youtube, resolved.Id, url);
    }

    /// <summary>
    /// Makes the playlist and queues its first sync, which reads it as <paramref name="reading"/> says.
    /// </summary>
    public async Task<M.Changed<M.PlaylistDetails>> ImportAsync(Guid me, Origin origin, OriginReading reading, CancellationToken cancellationToken)
    {
        var service = Views.Map<MusicService>(origin.Service);
        var playlistId = origin.PlaylistId;
        var existing = await db.Playlists.Where(x => x.ImportedById == me && x.OriginService == service && x.OriginPlaylistId == playlistId)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (existing != null)
            throw ShelfException.Conflict(M.PlaylistErrorCodes.AlreadyImported, new Dictionary<string, string> { ["playlistId"] = existing.Value.ToString() });

        var now = time.GetUtcNow();
        // named, until the first sync names it, by what it was imported by
        var title = origin.Url ?? playlistId;
        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            OwnerId = me,
            Title = title.Length > PlaylistService.MaxTitleLength ? title[..PlaylistService.MaxTitleLength] : title,
            Access = PlaylistAccess.Private,
            AddPolicy = PlaylistAddPolicy.AllMembers,
            SearchFirst = service,
            SystemKind = PlaylistSystemKind.None,
            OriginService = service,
            OriginPlaylistId = playlistId,
            OriginUrl = origin.Url,
            ImportedById = me,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Playlists.Add(playlist);
        db.PlaylistMembers.Add(new() { Id = Guid.NewGuid(), PlaylistId = playlist.Id, AccountId = me, Role = PlaylistRole.Owner, JoinedAt = now });

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var steps = UndoTokens.Capture(db);
        await db.SaveChangesAsync(cancellationToken);
        await jobs.EnqueueAsync(me, PlaylistService.SyncJobKind, playlist.Id.ToString(), new Input(playlist.Id, reading), cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new() { Value = await playlists.GetAsync(me, playlist.Id, null, cancellationToken), Undo = undo.Issue(me, steps) };
    }

    /// <summary>
    /// Starts a sync by the person's button in the app, unless one is under way: read as the importer.
    /// </summary>
    public async Task<M.PlaylistDetails> StartAsync(Guid me, Guid playlistId, CancellationToken cancellationToken)
    {
        var view = await gate.ForChangeAsync(me, playlistId, cancellationToken);
        if (view.Playlist.OriginService == null || view.Playlist.ImportedById != me)
            throw ShelfException.Forbidden(M.PlaylistErrorCodes.NotImportedByYou);

        if (await jobs.FindActiveAsync(PlaylistService.SyncJobKind, playlistId.ToString(), cancellationToken) == null)
            await jobs.EnqueueAsync(me, PlaylistService.SyncJobKind, playlistId.ToString(), new Input(playlistId, OriginReading.AsImporter), cancellationToken);

        return await playlists.GetAsync(me, playlistId, null, cancellationToken);
    }
}

/// <summary>
/// One sync of an imported playlist: the tracks that are new there are added, as songs with their recording; the songs
/// whose tracks were removed there stay; the songs removed here do not come back (their tracks are remembered); what
/// was changed here stays. Asked by its id (the one its link names, when it was imported by a link) as its input says
/// (<see cref="OriginReading"/>): as the importer, with their own music account when they linked one, otherwise
/// anonymously (the music service reads no playlist by its service account). Run again from the start, it adds nothing twice.
/// </summary>
public sealed class OriginSyncHandler(ShelfDbContext db, AudioClient audio, MusicLinkStore musicLinks, ItemRows itemRows, TimeProvider time, ILogger<OriginSyncHandler> logger)
    : IJobHandler
{
    public async Task<JobCompletion> RunAsync(JobRun run, CancellationToken cancellationToken)
    {
        var input = run.Input<OriginSync.Input>();
        var playlistId = input.PlaylistId;
        var playlist = await db.Playlists.SingleOrDefaultAsync(x => x.Id == playlistId, cancellationToken);
        if (playlist?.OriginService == null)
            throw new JobFailedException(M.PlaylistErrorCodes.NoSuchPlaylist);

        var service = playlist.OriginService.Value;
        var importer = playlist.ImportedById ?? run.AccountId;
        AudioPlaylistPage page;
        try
        {
            var acting = input.Reading == OriginReading.AsImporter
                ? await musicLinks.GetActingAsync(importer, Views.Map<Model.Me.MusicService>(service), cancellationToken) ?? AudioActing.Anonymous
                : AudioActing.Anonymous;
            var audioService = service == MusicService.Yandex ? AudioServiceName.Yandex : AudioServiceName.Youtube;
            page = (await audio.GetPlaylistAsync(audioService, playlist.OriginPlaylistId!, acting, cancellationToken)).Data;
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
