using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Api.Common;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Import;
using HarmonyDB.Shelf.Model.Jobs;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Areas.Import.Services;

/// <summary>
/// Imports links to pages of chords that the sources know, as a job: each link is resolved to chords by the sources,
/// for the person, and the chords become songs of the person's playlist of imported chords.
/// </summary>
public sealed class ImportService(JobQueue jobs, SourceCallers callers)
{
    public const string JobKind = "import";

    public const int MaxLinks = 100;

    public async Task<JobInfo> StartAsync(ClaimsPrincipal user, ImportLinksRequest request, CancellationToken cancellationToken)
    {
        var links = Split(request.Links ?? []);
        if (links.Count is 0 or > MaxLinks)
            throw ShelfException.BadRequest(ImportErrorCodes.BadLinks, new Dictionary<string, string> { ["max"] = MaxLinks.ToString() });

        return await jobs.EnqueueAsync(user.GetAccountId()!.Value, JobKind, null, new ImportInput(links, callers.For(user).Keys), cancellationToken);
    }

    /// <summary>
    /// The links given, each line of each apart, trimmed, without empty lines and repeats.
    /// </summary>
    internal static IReadOnlyList<string> Split(IReadOnlyList<string> links)
        => links
            .SelectMany(x => (x ?? string.Empty).Split('\n'))
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <param name="Keys">The custom keys of the person when they asked: the links are resolved for them as then.</param>
    internal sealed record ImportInput(IReadOnlyList<string> Links, IReadOnlyList<string> Keys);
}

/// <summary>
/// Resolves the links and puts the chords into the person's playlist of imported chords. Stages: <c>resolving</c>,
/// <c>adding</c>.
/// </summary>
public sealed class ImportJob(ShelfDbContext db, IndexClient index, SystemPlaylists systemPlaylists, SongService songs) : IJobHandler
{
    /// <summary>
    /// How many links are asked of the sources at once.
    /// </summary>
    private const int Chunk = 20;

    public async Task<JobCompletion> RunAsync(JobRun run, CancellationToken cancellationToken)
    {
        var input = run.Input<ImportService.ImportInput>();
        var caller = new Caller { Subjects = await AccountSubjects.LoadAsync(db, run.AccountId, cancellationToken), Keys = input.Keys };

        var states = new Dictionary<string, (ImportLinkState State, string? ChordsId)>(StringComparer.Ordinal);
        var uris = new List<(string Link, Uri Uri)>();
        foreach (var link in input.Links)
        {
            if (Uri.TryCreate(link, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
                uris.Add((link, uri));
            else
                states[link] = (ImportLinkState.NotALink, null);
        }

        foreach (var chunk in uris.Chunk(Chunk))
        {
            await run.ReportAsync("resolving", (double)states.Count / input.Links.Count / 2, cancellationToken);
            var resolved = await index.Sources.Resolve(caller, chunk.Select(x => x.Uri).ToList(), cancellationToken);
            foreach (var (link, answer) in chunk.Zip(resolved.Items, (x, y) => (x.Link, y)))
            {
                states[link] = answer.State switch
                {
                    ResolveState.Resolved => (ImportLinkState.Imported, answer.Id),
                    ResolveState.NotResolved => (ImportLinkState.NotFound, null),
                    ResolveState.Unavailable => (ImportLinkState.Unavailable, null),
                    _ => throw new ArgumentOutOfRangeException(nameof(run), answer.State, null),
                };
            }
        }

        await run.ReportAsync("adding", 0.5, cancellationToken);
        var playlist = await systemPlaylists.GetOrCreateAsync(run.AccountId, PlaylistSystemKind.ImportedChords, cancellationToken);
        var ids = states.Values.Where(x => x.ChordsId != null).Select(x => x.ChordsId!).Distinct(StringComparer.Ordinal).ToList();
        var songIds = await PutAsync(run, caller, playlist.Id, ids, cancellationToken);
        foreach (var (link, (state, chordsId)) in states.ToList())
        {
            // its header was not given by the time it was to be put: it is told as such
            if (chordsId != null && songIds[chordsId] is { Song: null } lost)
                states[link] = (lost.State, null);
        }

        return new(new ImportResult
        {
            PlaylistId = playlist.Id,
            Links = input.Links.Select(link =>
            {
                var (state, chordsId) = states[link];
                return new ImportLinkResult
                {
                    Link = link,
                    State = state,
                    ChordsId = chordsId,
                    SongId = chordsId == null ? null : songIds[chordsId].Song,
                };
            }).ToList(),
        });
    }

    /// <summary>
    /// Puts each of the chords into the playlist as a song, titled as its source titles it for the person, unless it is
    /// there already. A run again (after an interruption) finds those put before.
    /// </summary>
    /// <returns>The song each is in; for one whose header is not given now, no song and why.</returns>
    private async Task<Dictionary<string, (Guid? Song, ImportLinkState State)>> PutAsync(JobRun run, Caller caller, Guid playlistId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var songIds = await db.PlaylistSongItems
            .Where(x => x.Song.PlaylistId == playlistId && x.Song.DeletedAt == null && ids.Contains(x.Chords!.SourceId))
            .Select(x => new { x.Chords!.SourceId, x.SongId })
            .ToDictionaryAsync(x => x.SourceId, x => ((Guid?)x.SongId, ImportLinkState.Imported), StringComparer.Ordinal, cancellationToken);

        var missing = ids.Where(x => !songIds.ContainsKey(x)).ToList();
        var headers = missing.Count == 0
            ? []
            : (await index.Sources.GetHeaders(caller, missing, cancellationToken)).Headers.ToDictionary(x => x.Id, StringComparer.Ordinal);

        var added = 0;
        foreach (var id in missing)
        {
            var answer = headers[id];
            if (answer.State != ItemState.Open)
            {
                songIds[id] = (null, answer.State == ItemState.NotOpen ? ImportLinkState.NotFound : ImportLinkState.Unavailable);
                continue;
            }

            var song = await songs.AddAsync(run.AccountId, playlistId, new()
            {
                Chords = id,
                Title = answer.Header!.Title,
                Artists = answer.Header.Artists,
            }, cancellationToken);
            songIds[id] = (song.Value.Id, ImportLinkState.Imported);
            await run.ReportAsync("adding", 0.5 + (double)++added / missing.Count / 2, cancellationToken);
        }

        return songIds;
    }
}
