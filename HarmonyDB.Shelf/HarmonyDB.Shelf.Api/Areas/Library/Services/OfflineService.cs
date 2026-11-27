using HarmonyDB.Shelf.Api.Areas.Chords;
using HarmonyDB.Shelf.Api.Areas.Book.Services;
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
/// The person's library for their device, which keeps it always: the library's chords with the address each is read by,
/// told again only when something that makes up the library changed (by the versions the sync's watermark is of), and
/// their content, a page at a time, as the sources give it to the person. Only the library's chords are read: the page
/// is not open to any other id.
/// <para>
/// What the device keeps of an item is never read again: the sources give no version or time of an item's content, and
/// the device keeps what it was given until the person clears it — as the old shelf kept chords on its device. Own
/// chords their author edited come with a new address: to the device they are new, and taken anew.
/// </para>
/// </summary>
public sealed class OfflineService(ShelfDbContext db, SongLists lists, IndexClient index, SourceCallers callers)
{
    /// <summary>
    /// How many items of a page are read at once.
    /// </summary>
    private const int Parallelism = 4;

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
            Chords = (await lists.LibraryChordsAsync(me, cancellationToken))
                .Select(x => new M.OfflineChords { Id = x.Chords.SourceId, Address = x.Address })
                .OrderBy(x => x.Id, StringComparer.Ordinal)
                .ToList(),
        };
    }

    /// <summary>
    /// The content of chords of the library, each read by the address the library keeps for it: one by one, a few at
    /// once (a source's batch read takes no addresses).
    /// </summary>
    public async Task<M.OfflineContents> GetContentsAsync(ClaimsPrincipal user, M.OfflineContentsRequest request, CancellationToken cancellationToken)
    {
        var ids = request.Chords;
        if (ids is not { Count: > 0 and <= M.OfflineContentsRequest.MaxChords } || ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            throw ShelfException.BadRequest(ErrorCodes.BadRequest, new Dictionary<string, string> { ["max"] = M.OfflineContentsRequest.MaxChords.ToString() });

        var kinds = (request.Kinds is { Count: > 0 } asked ? asked : [ChordsKind.Sheet]).Distinct().ToList();
        if (kinds.Any(x => !M.OfflineContentsRequest.KeptKinds.Contains(x)))
            throw ShelfException.BadRequest();

        var library = (await lists.LibraryChordsAsync(user.GetAccountId()!.Value, cancellationToken))
            .ToDictionary(x => x.Chords.SourceId, x => x.Address, StringComparer.Ordinal);
        var caller = callers.For(user);
        var contentKinds = kinds.Select(x => Enum.Parse<ContentKind>(x.ToString())).ToList();
        var items = await BookPrinter.ForEachAsync(ids, async (id, token) =>
        {
            if (!library.TryGetValue(id, out var address))
                return new M.OfflineContent { Chords = id, State = M.OfflineContentState.NotInLibrary };

            var answer = await index.Sources.GetContent(caller, id, contentKinds, ChordsArea.ParseAddress(address), token);
            return new M.OfflineContent
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
            };
        }, Parallelism, cancellationToken);

        return new() { Items = items };
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
