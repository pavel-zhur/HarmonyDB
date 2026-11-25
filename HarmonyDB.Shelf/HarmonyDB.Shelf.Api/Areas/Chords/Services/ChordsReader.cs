using System.Security.Claims;
using System.Text.Json;
using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Api.Common;
using HarmonyDB.Shelf.Api.Common.Items;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.EntityFrameworkCore;
using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.Extensions.Caching.Memory;

namespace HarmonyDB.Shelf.Api.Areas.Chords.Services;

/// <summary>
/// Items of chords as the sources give them to a person, through the index's relay, with the person's account and
/// keys. What the source answers is carried through as it is: open, not open, unavailable.
/// The answer for an item (its state, and its header with the source's label) is kept for a while per item, set of
/// keys and account — a source decides by the keys and may decide by the account — so it never passes from one person
/// to another. An item that did not answer is not kept. With the items come the person's own marks, keys and notes on
/// them (<see cref="ChordsAnnotations"/>).
/// </summary>
public sealed class ChordsReader(
    ShelfDbContext db, IndexClient index, SourceCallers callers, ItemRows rows, ChordsAnnotations annotations, IMemoryCache cache)
{
    public const int MaxIds = 200;

    private static readonly TimeSpan Keep = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The state and header of each item for the person, in the order asked.
    /// </summary>
    public async Task<IReadOnlyList<ChordsAnswer>> GetHeadersAsync(ClaimsPrincipal user, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        if (ids.Count > MaxIds)
            throw ShelfException.BadRequest(ChordsErrorCodes.TooManyIds, new Dictionary<string, string> { ["max"] = MaxIds.ToString() });
        if (ids.Any(string.IsNullOrWhiteSpace))
            throw ShelfException.BadRequest();

        return (await GetAnswersAsync(callers.For(user), ids, cancellationToken)).Select(ToAnswer).ToList();
    }

    /// <summary>
    /// The state and header of each item for the person, with the person's own marks, keys and notes on them: for a
    /// list of chords in one call.
    /// </summary>
    public async Task<ChordsHeadersResponse> GetHeadersWithAnnotationsAsync(ClaimsPrincipal user, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var items = await GetHeadersAsync(user, ids, cancellationToken);
        var accountId = user.GetAccountId();
        var local = accountId == null
            ? []
            : await db.Chords.AsNoTracking().Where(x => ids.Contains(x.SourceId)).Select(x => x.Id).ToListAsync(cancellationToken);
        var seen = await annotations.ReadAsync(accountId, local, null, null, cancellationToken);
        return new() { Items = items, Marks = seen.Marks, Keys = seen.Keys, MyNotes = seen.MyNotes };
    }

    /// <summary>
    /// The item for the person: its state, and when it is open, its local row and the header the person was given
    /// (what the person sees of it — the row keeps only what an anonymous caller sees, <see cref="ItemRows"/>).
    /// </summary>
    public async Task<(ChordsState State, ChordsItem? Row, Header? Header)> GetOpenRowAsync(ClaimsPrincipal user, string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw ShelfException.BadRequest();

        var caller = callers.For(user);
        var answer = (await GetAnswersAsync(caller, [id], cancellationToken)).Single();
        return answer.State == ItemState.Open
            ? (ChordsState.Open, await rows.GetOrCreateChordsAsync(id, answer.Header, IsAnonymous(caller), cancellationToken), answer.Header)
            : (ToState(answer.State), null, null);
    }

    /// <summary>
    /// The person's answers for the items, in the order asked (the source contract's own).
    /// </summary>
    public Task<IReadOnlyList<HeaderAnswer>> GetSourceHeadersAsync(ClaimsPrincipal user, IReadOnlyList<string> ids, CancellationToken cancellationToken)
        => GetAnswersAsync(callers.For(user), ids, cancellationToken);

    /// <summary>
    /// Whether the caller has no keys and no account: only such a caller's header is kept in a row.
    /// </summary>
    public static bool IsAnonymous(Caller caller) => caller.Keys.Count == 0 && caller.AccountId == null;

    private async Task<IReadOnlyList<HeaderAnswer>> GetAnswersAsync(Caller caller, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var known = new Dictionary<string, HeaderAnswer>(StringComparer.Ordinal);
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            if (cache.TryGetValue(CacheKey(caller, id), out HeaderAnswer? answer))
                known[id] = answer!;
        }

        var missing = ids.Distinct(StringComparer.Ordinal).Where(x => !known.ContainsKey(x)).ToList();
        if (missing.Count > 0)
        {
            var response = await index.Sources.GetHeaders(caller, missing, cancellationToken);
            foreach (var answer in response.Headers)
            {
                known[answer.Id] = answer;
                Remember(caller, answer);
            }
        }

        return ids.Select(x => known[x]).ToList();
    }

    /// <summary>
    /// The item for the person with the kinds asked; when it is open, its local row is made or refreshed.
    /// </summary>
    public async Task<ChordsContentResponse> GetContentAsync(ClaimsPrincipal user, string id, IReadOnlyList<ContentKind> kinds, CancellationToken cancellationToken)
    {
        var caller = callers.For(user);
        var response = await index.Sources.GetContent(caller, id, kinds, cancellationToken);
        Remember(caller, new() { Id = id, State = response.State, Header = response.Header });
        if (response.State != ItemState.Open)
            return new() { Id = id, State = ToState(response.State), Marks = [], Keys = [], MyNotes = [] };

        var row = await rows.GetOrCreateChordsAsync(id, response.Header, IsAnonymous(caller), cancellationToken);
        var seen = await annotations.ReadAsync(user.GetAccountId(), [row.Id], null, null, cancellationToken);
        return new()
        {
            Id = id,
            State = ChordsState.Open,
            ChordsId = row.Id,
            Header = ToHeader(response.Header!),
            Content = response.Content,
            Marks = seen.Marks,
            Keys = seen.Keys,
            MyNotes = seen.MyNotes,
        };
    }

    /// <summary>
    /// The item's content for the person, in the source contract's own models: for the areas that read a kind
    /// themselves (the media of a recording). Null when the item is not open to the person.
    /// </summary>
    /// <exception cref="ShelfException">503 <c>unavailable</c>: its source did not answer.</exception>
    public async Task<Content?> GetOpenContentAsync(ClaimsPrincipal user, string id, IReadOnlyList<ContentKind> kinds, CancellationToken cancellationToken)
    {
        var caller = callers.For(user);
        var response = await index.Sources.GetContent(caller, id, kinds, cancellationToken);
        Remember(caller, new() { Id = id, State = response.State, Header = response.Header });
        return response.State switch
        {
            ItemState.Open => response.Content,
            ItemState.NotOpen => null,
            _ => throw new ShelfException(StatusCodes.Status503ServiceUnavailable, Model.ErrorCodes.Unavailable),
        };
    }

    public static ChordsAnswer ToAnswer(HeaderAnswer answer) => new()
    {
        Id = answer.Id,
        State = ToState(answer.State),
        Header = answer.Header == null ? null : ToHeader(answer.Header),
    };

    public static ChordsState ToState(ItemState state) => state switch
    {
        ItemState.Open => ChordsState.Open,
        ItemState.NotOpen => ChordsState.NotOpen,
        ItemState.Unavailable => ChordsState.Unavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    public static ChordsHeader ToHeader(Header header) => new()
    {
        Id = header.Id,
        Source = new()
        {
            Key = header.Source.Key,
            Title = header.Source.Title,
            Uri = header.Source.Uri?.ToString(),
            IconUrl = header.Source.Icon == null ? null : IconUrl(header.Source.Icon),
        },
        Title = header.Title,
        Artists = header.Artists,
        Rating = header.Rating,
        Type = header.Type,
        Kinds = header.Kinds.Select(ToKind).ToList(),
        Attributes = new()
        {
            Key = header.Attributes.Key,
            Bpm = header.Attributes.Bpm,
            Capo = header.Attributes.Capo,
            TimeSignature = header.Attributes.TimeSignature,
        },
        IsPublic = header.IsPublic,
    };

    public static string IconUrl(string name) => $"/api/chords/icons/{Uri.EscapeDataString(name)}";

    public static ChordsKind ToKind(ContentKind kind) => Enum.Parse<ChordsKind>(kind.ToString());

    public static ContentKind ToContentKind(ChordsKind kind) => Enum.Parse<ContentKind>(kind.ToString());

    private void Remember(Caller caller, HeaderAnswer answer)
    {
        if (answer.State != ItemState.Unavailable)
            cache.Set(CacheKey(caller, answer.Id), answer, Keep);
    }

    private static (string, string, string, Guid?) CacheKey(Caller caller, string id)
        => ("chords-header", id, string.Join('\n', caller.Keys), caller.AccountId);
}
