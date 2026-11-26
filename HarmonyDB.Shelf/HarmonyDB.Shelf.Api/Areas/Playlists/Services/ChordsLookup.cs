using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Common.Items;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Chords;
using Microsoft.EntityFrameworkCore;
using PlaylistErrorCodes = HarmonyDB.Shelf.Model.Playlists.PlaylistErrorCodes;

namespace HarmonyDB.Shelf.Api.Areas.Playlists.Services;

/// <summary>
/// Chords by the id the API knows them by (the source's opaque id), as their source gives them to one person, with
/// their local row. The source is asked for the person (with their permissions as stored: the same from a request and
/// from a job; the answer is kept a while by the chords' reader).
/// <list type="bullet">
/// <item>Open to the person: the row (made when there is none) and the header the person got.</item>
/// <item>Not open to the person (or not known: the two cannot be told apart): no row is ever made for it, and a person
/// puts into a playlist only chords open to them (403 <c>chords_not_open</c>). Chords already held here (in a shared
/// playlist) may still be marked or given a key or a note by someone they are not open to: for that the source is not
/// asked at all.</item>
/// <item>The source does not answer: nothing is made (503 <c>unavailable</c>).</item>
/// </list>
/// What the person is told of the chords is only what the source told them, never the row's cache.
/// </summary>
public sealed class ChordsLookup(ShelfDbContext db, ChordsReader reader, ItemRows rows)
{
    /// <summary>
    /// The longest source id taken.
    /// </summary>
    public const int MaxSourceIdLength = 200;

    /// <param name="requireOpen">For putting the chords into a playlist: they must be open to the person.</param>
    /// <returns>The row, and the header the person was given when the source was asked (and the chords are open to
    /// them).</returns>
    /// <exception cref="ShelfException">400 <c>bad_request</c>: a text that is no id; 403 <c>chords_not_open</c>; 503
    /// <c>unavailable</c>: its source did not answer.</exception>
    public async Task<(ChordsItem Row, ChordsHeader? Header)> GetOrCreateAsync(Guid accountId, string? sourceId, bool requireOpen, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || sourceId.Length > MaxSourceIdLength || sourceId.Any(char.IsControl))
            throw ShelfException.BadRequest();

        // chords held here already, for what does not need them open (a mark, a key, a note): nothing to ask
        if (!requireOpen && await db.Chords.SingleOrDefaultAsync(x => x.SourceId == sourceId, cancellationToken) is { } known)
            return (known, null);

        var answer = (await reader.GetHeadersAsync(await PersonAsync(accountId, cancellationToken), [sourceId], cancellationToken)).Single();
        switch (answer.State)
        {
            case ChordsState.Open:
                return (await db.Chords.SingleOrDefaultAsync(x => x.SourceId == sourceId, cancellationToken)
                        ?? await rows.GetOrCreateChordsAsync(sourceId, null, false, cancellationToken), answer.Header);
            case ChordsState.NotOpen:
                throw ShelfException.Forbidden(PlaylistErrorCodes.ChordsNotOpen);
            default:
                throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
        }
    }

    /// <summary>
    /// The chords' header as their source gives it to the person now; null when they are not open to the person.
    /// </summary>
    /// <exception cref="ShelfException">503 <c>unavailable</c>: its source did not answer.</exception>
    public async Task<ChordsHeader?> HeaderAsync(Guid accountId, string sourceId, CancellationToken cancellationToken)
    {
        var answer = (await reader.GetHeadersAsync(await PersonAsync(accountId, cancellationToken), [sourceId], cancellationToken)).Single();
        return answer.State == ChordsState.Unavailable
            ? throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable)
            : answer.Header;
    }

    /// <summary>
    /// Which of the chords are open to the person (by the source's ids).
    /// </summary>
    /// <exception cref="ShelfException">503 <c>unavailable</c>: a source did not answer for one of them.</exception>
    public async Task<IReadOnlySet<string>> OpenAsync(Guid accountId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken)
    {
        var open = new HashSet<string>(StringComparer.Ordinal);
        if (sourceIds.Count == 0)
            return open;

        var person = await PersonAsync(accountId, cancellationToken);
        foreach (var chunk in sourceIds.Distinct(StringComparer.Ordinal).Chunk(ChordsReader.MaxIds))
        {
            foreach (var answer in await reader.GetHeadersAsync(person, chunk, cancellationToken))
            {
                if (answer.State == ChordsState.Unavailable)
                    throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
                if (answer.State == ChordsState.Open)
                    open.Add(answer.Id);
            }
        }

        return open;
    }

    /// <summary>
    /// The person as a request of theirs would be (their permissions as stored), for what is done for them outside one.
    /// </summary>
    public async Task<ClaimsPrincipal> PersonAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var permissions = await db.AccountPermissions.Where(x => x.AccountId == accountId).Select(x => x.Permission).ToListAsync(cancellationToken);
        return new(ShelfIdentity.Create(nameof(ChordsLookup), accountId, await AccountSubjects.LoadAsync(db, accountId, cancellationToken), permissions, null));
    }
}
