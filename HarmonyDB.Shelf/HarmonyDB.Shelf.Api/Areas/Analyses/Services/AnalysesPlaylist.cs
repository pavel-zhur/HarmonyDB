using HarmonyDB.Shelf.Api.Common.Items;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Areas.Analyses.Services;

/// <summary>
/// A public playlist of a system account that holds analyses: a song for each, its one variant the analysis's chords.
/// Written directly on the model, the least the playlists need: the system account, the playlist with its owner's
/// member row, the songs and their variants; the chords rows are made by <see cref="ItemRows"/>, with their headers as a
/// caller with no keys sees them.
/// </summary>
public sealed class AnalysesPlaylist(ShelfDbContext db, AnalysesSource source, ItemRows itemRows, TimeProvider time)
{
    public async Task<PublishAnalysesPlaylistResponse> PublishAsync(PublishAnalysesPlaylistRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SystemAccount) || string.IsNullOrWhiteSpace(request.Title) || request.Ids.Distinct().Count() != request.Ids.Count)
            throw ShelfException.BadRequest();

        var analyses = await db.Analyses.Where(x => request.Ids.Contains(x.SourceId)).ToDictionaryAsync(x => x.SourceId, cancellationToken);
        foreach (var id in request.Ids)
        {
            var analysis = analyses.GetValueOrDefault(id) ?? throw ShelfException.NotFound(AnalysesErrorCodes.NoSuchAnalysis);
            if (analysis.OwnerId != null)
                throw ShelfException.BadRequest(AnalysesErrorCodes.AnalysisNotPublic, new Dictionary<string, string> { ["id"] = id });
        }

        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var account = await db.Accounts.SingleOrDefaultAsync(x => x.SystemName == request.SystemAccount, cancellationToken);
        if (account == null)
        {
            account = new() { Id = Guid.NewGuid(), DisplayName = request.SystemAccount, SystemName = request.SystemAccount, CreatedAt = now };
            db.Accounts.Add(account);
        }

        var playlist = await db.Playlists.SingleOrDefaultAsync(x => x.OwnerId == account.Id && x.Title == request.Title, cancellationToken);
        if (playlist == null)
        {
            playlist = new()
            {
                Id = Guid.NewGuid(),
                OwnerId = account.Id,
                Title = request.Title,
                AddPolicy = PlaylistAddPolicy.Admins,
                SystemKind = PlaylistSystemKind.None,
                CreatedAt = now,
            };
            db.Playlists.Add(playlist);
            db.PlaylistMembers.Add(new()
            {
                Id = Guid.NewGuid(),
                PlaylistId = playlist.Id,
                AccountId = account.Id,
                Role = PlaylistRole.Owner,
                JoinedAt = now,
            });
        }

        playlist.Description = request.Description;
        playlist.Access = PlaylistAccess.Public;
        playlist.UpdatedAt = now;

        // the chords items, with their headers as a caller with no keys sees them
        var headers = (await source.HeadersAsync(request.Ids.Select(x => analyses[x]).ToList(), cancellationToken)).ToDictionary(x => x.Id);
        var items = new Dictionary<string, ChordsItem>();
        foreach (var id in request.Ids)
            items[id] = await itemRows.GetOrCreateChordsAsync(id, headers[id], anonymous: true, cancellationToken);

        // the songs: one for each analysis, in the order given; those of other chords are removed
        // the live songs and variants only (the query filters say so too): a removed song stays removed
        var songs = await db.PlaylistSongs.Where(x => x.PlaylistId == playlist.Id && x.DeletedAt == null)
            .Include(x => x.Items.Where(i => i.DeletedAt == null)).ToListAsync(cancellationToken);
        var bySource = songs
            .SelectMany(song => song.Items.Where(x => x.ChordsId != null).Select(x => (song, x.ChordsId)))
            .ToLookup(x => x.ChordsId!.Value, x => x.song);

        int added = 0, removed = 0;
        var kept = new HashSet<Guid>();
        for (var i = 0; i < request.Ids.Count; i++)
        {
            var analysis = analyses[request.Ids[i]];
            var item = items[request.Ids[i]];
            var song = bySource[item.Id].FirstOrDefault(x => !kept.Contains(x.Id));
            if (song == null)
            {
                song = new()
                {
                    Id = Guid.NewGuid(),
                    PlaylistId = playlist.Id,
                    Title = analysis.Title,
                    AddedById = account.Id,
                    AddedAt = now,
                };
                db.PlaylistSongs.Add(song);
                db.PlaylistSongItems.Add(new()
                {
                    Id = Guid.NewGuid(),
                    SongId = song.Id,
                    ChordsId = item.Id,
                    Position = 0,
                    AddedById = account.Id,
                    AddedAt = now,
                });
                added++;
            }

            song.Title = analysis.Title;
            song.Artists = analysis.Artists.ToList();
            song.Position = i + 1;
            kept.Add(song.Id);
        }

        foreach (var song in songs.Where(x => !kept.Contains(x.Id)))
        {
            song.DeletedAt = now;
            removed++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new()
        {
            PlaylistId = playlist.Id,
            AccountId = account.Id,
            Songs = request.Ids.Count,
            Added = added,
            Removed = removed,
        };
    }
}
