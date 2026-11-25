using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Source.Api.Model.V2;
using Microsoft.EntityFrameworkCore;
using M = HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Api.Areas.Library.Services;

/// <summary>
/// The person's library for a device that keeps it, to open it without a connection: the ids of the library's chords,
/// read again only when something that makes up the library changed (by the versions the sync's watermark is of), and
/// their content, many at once, as the sources give it to the person (the source contract's batch read). Only the
/// library's chords are read: the batch is not open to any other id.
/// <para>
/// A change of an item's content: the sources give no version or time of an item's content, so the shelf cannot tell
/// that one changed. What the device keeps of an item is what it was given last; it is had anew when the person opens
/// the item online, or when the device reads its whole library again (<c>since</c> left out, and the contents of all).
/// What changes the library itself — an item added or removed, a playlist joined, left, followed or hidden, the
/// person's marks, keys and notes — is told by <see cref="ListAsync"/>; an item no longer open to the person is told by
/// the content read (<see cref="M.OfflineContentState.NotOpen"/>).
/// </para>
/// </summary>
public sealed class OfflineService(ShelfDbContext db, SongLists lists, IndexClient index, SourceCallers callers)
{
    public async Task<M.OfflineChordsList> ListAsync(ClaimsPrincipal user, long? since, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;
        var watermark = await db.GetChangeWatermarkAsync(cancellationToken);

        // a watermark this server never gave: everything
        if (since is { } from && from <= watermark && !await ChangedSinceAsync(me, from, cancellationToken))
            return new() { Watermark = watermark, Changed = false, Chords = [] };

        return new()
        {
            Watermark = watermark,
            Changed = true,
            Chords = (await lists.LibraryChordsAsync(me, cancellationToken)).Select(x => x.SourceId).Order(StringComparer.Ordinal).ToList(),
        };
    }

    public async Task<M.OfflineContents> GetContentsAsync(ClaimsPrincipal user, M.OfflineContentsRequest request, CancellationToken cancellationToken)
    {
        var ids = request.Chords;
        if (ids is not { Count: > 0 and <= M.OfflineContentsRequest.MaxChords } || ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            throw ShelfException.BadRequest(ErrorCodes.BadRequest, new Dictionary<string, string> { ["max"] = M.OfflineContentsRequest.MaxChords.ToString() });

        var kinds = (request.Kinds is { Count: > 0 } asked ? asked : [ChordsKind.Sheet]).Distinct().ToList();
        if (kinds.Contains(ChordsKind.Recording))
            throw ShelfException.BadRequest();

        var library = (await lists.LibraryChordsAsync(user.GetAccountId()!.Value, cancellationToken)).Select(x => x.SourceId).ToHashSet(StringComparer.Ordinal);
        var inLibrary = ids.Where(library.Contains).ToList();
        var answers = inLibrary.Count == 0
            ? []
            : (await index.Sources.GetContents(callers.For(user), inLibrary, kinds.Select(x => Enum.Parse<ContentKind>(x.ToString())).ToList(), cancellationToken))
            .Items.ToDictionary(x => x.Id, StringComparer.Ordinal);

        return new()
        {
            Items = ids.Select(id => answers.TryGetValue(id, out var answer)
                    ? new M.OfflineContent
                    {
                        Chords = id,
                        State = answer.State switch
                        {
                            ItemState.Open => M.OfflineContentState.Open,
                            ItemState.NotOpen => M.OfflineContentState.NotOpen,
                            _ => M.OfflineContentState.Unavailable,
                        },
                        Header = answer.Header == null ? null : ChordsReader.ToHeader(answer.Header),
                        Content = answer.Content,
                    }
                    : new M.OfflineContent { Chords = id, State = M.OfflineContentState.NotInLibrary })
                .ToList(),
        };
    }

    /// <summary>
    /// Whether anything that makes up the person's library changed since: their memberships and follows, the
    /// playlists they are or were in or follow, those playlists' songs and variants, and their own marks, keys and notes.
    /// </summary>
    private async Task<bool> ChangedSinceAsync(Guid me, long from, CancellationToken cancellationToken)
    {
        var playlists = db.PlaylistMembers.IgnoreQueryFilters().Where(x => x.AccountId == me).Select(x => x.PlaylistId)
            .Union(db.PlaylistFollows.IgnoreQueryFilters().Where(x => x.AccountId == me).Select(x => x.PlaylistId));

        return await db.PlaylistMembers.IgnoreQueryFilters().AnyAsync(x => x.AccountId == me && x.Version >= from, cancellationToken)
               || await db.PlaylistFollows.IgnoreQueryFilters().AnyAsync(x => x.AccountId == me && x.Version >= from, cancellationToken)
               || await db.Playlists.IgnoreQueryFilters().AnyAsync(x => playlists.Contains(x.Id) && x.Version >= from, cancellationToken)
               || await db.PlaylistSongs.IgnoreQueryFilters().AnyAsync(x => playlists.Contains(x.PlaylistId) && x.Version >= from, cancellationToken)
               || await db.PlaylistSongItems.IgnoreQueryFilters().AnyAsync(x => playlists.Contains(x.Song.PlaylistId) && x.Version >= from, cancellationToken)
               || await db.Marks.IgnoreQueryFilters().AnyAsync(x => x.AccountId == me && x.Version >= from, cancellationToken)
               || await db.Transpositions.IgnoreQueryFilters().AnyAsync(x => x.AccountId == me && x.Version >= from, cancellationToken)
               || await db.Notes.IgnoreQueryFilters().AnyAsync(x => x.AccountId == me && x.Version >= from, cancellationToken);
    }
}
