using HarmonyDB.Shelf.Api.Areas.Chords;
using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
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
/// their local row: for putting chords into a playlist, or marking them. The source is asked for the person (with their
/// permissions as stored: the same from a request and from a job), by the address they came by.
/// <list type="bullet">
/// <item>Open to the person: the row (made when there is none) and the header the person got.</item>
/// <item>Not open to the person (or not known: the two cannot be told apart): no row is ever made for it, and a person
/// puts into a playlist only chords open to them (403 <c>chords_not_open</c>). Chords already held here (in a shared
/// playlist) may still be marked or given a key or a note by someone they are not open to: for that the source is not
/// asked at all.</item>
/// <item>The source does not answer: nothing is made (503 <c>unavailable</c>).</item>
/// </list>
/// </summary>
public sealed class ChordsLookup(ShelfDbContext db, ChordsReader reader)
{
    /// <summary>
    /// The longest source id taken.
    /// </summary>
    public const int MaxSourceIdLength = 200;

    /// <param name="address">The address the person came by, if any (a link): the chords are read by it.</param>
    /// <param name="requireOpen">For putting the chords into a playlist: they must be open to the person.</param>
    /// <returns>The row; when the source was asked (and the chords are open to the person), the header the person was
    /// given and the address the playlist keeps for them: the one the person came by, else the header's.</returns>
    /// <exception cref="ShelfException">400 <c>bad_request</c>: a text that is no id; 403 <c>chords_not_open</c>; 503
    /// <c>unavailable</c>: its source did not answer.</exception>
    public async Task<(ChordsItem Row, ChordsHeader? Header, string? Address)> GetOrCreateAsync(Guid accountId, string? sourceId, string? address, bool requireOpen, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || sourceId.Length > MaxSourceIdLength || sourceId.Any(char.IsControl))
            throw ShelfException.BadRequest();
        var by = ChordsArea.ParseAddress(address);

        // chords held here already, for what does not need them open (a mark, a key, a note): nothing to ask
        if (!requireOpen && await db.Chords.SingleOrDefaultAsync(x => x.SourceId == sourceId, cancellationToken) is { } known)
            return (known, null, null);

        // by the id: the source's header; by an address: the item read by it (a header takes no address)
        var person = await PersonAsync(accountId, cancellationToken);
        var (state, header) = by == null
            ? await HeaderAsync(person, sourceId, cancellationToken)
            : await reader.GetContentAsync(person, sourceId, [], by, cancellationToken) is var answer ? (answer.State, answer.Header) : default;
        return state switch
        {
            ChordsState.Open => (await db.Chords.SingleAsync(x => x.SourceId == sourceId, cancellationToken), header, by?.ToString() ?? header!.Address),
            ChordsState.NotOpen => throw ShelfException.Forbidden(PlaylistErrorCodes.ChordsNotOpen),
            _ => throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable),
        };
    }

    private async Task<(ChordsState State, ChordsHeader? Header)> HeaderAsync(ClaimsPrincipal person, string sourceId, CancellationToken cancellationToken)
    {
        var (state, _, header) = await reader.GetOpenRowAsync(person, sourceId, cancellationToken);
        return (state, header == null ? null : ChordsReader.ToHeader(header));
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
