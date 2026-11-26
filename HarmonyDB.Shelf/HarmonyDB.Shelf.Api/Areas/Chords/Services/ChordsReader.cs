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
/// Items of chords as the sources give them to a person, through the index's relay, with the person's names and keys,
/// and the address the person came by where there is one. What the source answers is carried through as it is: open,
/// not open, unavailable.
/// The answer for an item (its state, and its header with the source's label) is kept for a while per item, names, keys
/// and address — a source decides by all of them — so it never passes from one person to another, nor from one who came
/// by a link to one who came by the id. An item that did not answer is not kept. With the items come the person's own marks, keys and notes on
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
    public Task<IReadOnlyList<ChordsAnswer>> GetHeadersAsync(ClaimsPrincipal user, IReadOnlyList<string> ids, CancellationToken cancellationToken)
        => GetHeadersAsync(callers.For(user), ids, cancellationToken);

    /// <summary>
    /// The state and header of each item for the caller given, in the order asked.
    /// </summary>
    public async Task<IReadOnlyList<ChordsAnswer>> GetHeadersAsync(Caller caller, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        if (ids.Count > MaxIds)
            throw ShelfException.BadRequest(ChordsErrorCodes.TooManyIds, new Dictionary<string, string> { ["max"] = MaxIds.ToString() });
        if (ids.Any(string.IsNullOrWhiteSpace))
            throw ShelfException.BadRequest();

        return (await GetAnswersAsync(caller, ids, cancellationToken)).Select(ToAnswer).ToList();
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
            ? (ChordsState.Open, await rows.GetOrCreateChordsAsync(id, answer.Header, IsAnonymous(caller, null), cancellationToken), answer.Header)
            : (ToState(answer.State), null, null);
    }

    /// <summary>
    /// The person's answers for the items, in the order asked (the source contract's own).
    /// </summary>
    public Task<IReadOnlyList<HeaderAnswer>> GetSourceHeadersAsync(ClaimsPrincipal user, IReadOnlyList<string> ids, CancellationToken cancellationToken)
        => GetAnswersAsync(callers.For(user), ids, cancellationToken);

    /// <summary>
    /// Whether the caller has no keys and no names, and came by no address: only such a caller's header is kept in a row.
    /// </summary>
    public static bool IsAnonymous(Caller caller, Uri? address) => caller.Keys.Count == 0 && caller.Subjects.Count == 0 && address == null;

    private async Task<IReadOnlyList<HeaderAnswer>> GetAnswersAsync(Caller caller, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var known = new Dictionary<string, HeaderAnswer>(StringComparer.Ordinal);
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            if (cache.TryGetValue(CacheKey(caller, id, null), out HeaderAnswer? answer))
                known[id] = answer!;
        }

        var missing = ids.Distinct(StringComparer.Ordinal).Where(x => !known.ContainsKey(x)).ToList();
        if (missing.Count > 0)
        {
            var response = await index.Sources.GetHeaders(caller, missing, cancellationToken);
            foreach (var answer in response.Headers)
            {
                known[answer.Id] = answer;
                Remember(caller, null, answer);
            }
        }

        return ids.Select(x => known[x]).ToList();
    }

    /// <summary>
    /// The item for the person with the kinds asked, who came by the address given (null: by the id); when it is open,
    /// its local row is made or refreshed.
    /// </summary>
    public Task<ChordsContentResponse> GetContentAsync(ClaimsPrincipal user, string id, IReadOnlyList<ContentKind> kinds, Uri? address, CancellationToken cancellationToken)
        => GetContentAsync(callers.For(user), user.GetAccountId(), id, kinds, address, cancellationToken);

    /// <summary>
    /// The item for the caller given with the kinds asked, with the marks, keys and notes <paramref name="viewer"/> sees
    /// on it; when it is open, its local row is made or refreshed.
    /// </summary>
    public async Task<ChordsContentResponse> GetContentAsync(Caller caller, Guid? viewer, string id, IReadOnlyList<ContentKind> kinds, Uri? address, CancellationToken cancellationToken)
    {
        var response = await index.Sources.GetContent(caller, id, kinds, address, cancellationToken);
        Remember(caller, address, new() { Id = id, State = response.State, Header = response.Header });
        if (response.State != ItemState.Open)
            return new() { Id = id, State = ToState(response.State), Marks = [], Keys = [], MyNotes = [] };

        var row = await rows.GetOrCreateChordsAsync(id, response.Header, IsAnonymous(caller, address), cancellationToken);
        var seen = await annotations.ReadAsync(viewer, [row.Id], null, null, cancellationToken);
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
    public Task<Content?> GetOpenContentAsync(ClaimsPrincipal user, string id, IReadOnlyList<ContentKind> kinds, Uri? address, CancellationToken cancellationToken)
        => GetOpenContentAsync(callers.For(user), id, kinds, address, cancellationToken);

    /// <summary>
    /// The item's content for the caller given; null when it is not open to them.
    /// </summary>
    /// <exception cref="ShelfException">503 <c>unavailable</c>: its source did not answer.</exception>
    public async Task<Content?> GetOpenContentAsync(Caller caller, string id, IReadOnlyList<ContentKind> kinds, Uri? address, CancellationToken cancellationToken)
    {
        var response = await index.Sources.GetContent(caller, id, kinds, address, cancellationToken);
        Remember(caller, address, new() { Id = id, State = response.State, Header = response.Header });
        return response.State switch
        {
            ItemState.Open => response.Content,
            ItemState.NotOpen => null,
            _ => throw new ShelfException(StatusCodes.Status503ServiceUnavailable, Model.ErrorCodes.Unavailable),
        };
    }

    /// <summary>
    /// The sources the person may see, as their labels show them.
    /// </summary>
    public async Task<ChordsSourcesResponse> GetSourcesAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var response = await index.Sources.GetSources(callers.For(user), cancellationToken);
        return new()
        {
            Sources = response.Sources.Select(ToSource).ToList(),
            Incomplete = response.Failures.Count > 0,
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
        Source = ToSource(header.Source),
        Address = header.Address?.ToString(),
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

    public static ChordsSource ToSource(SourceLabel label) => new()
    {
        Key = label.Key,
        Title = label.Title,
        IconUrl = label.Icon == null ? null : IconUrl(label.Icon),
    };

    public static string IconUrl(string name) => $"/api/chords/icons/{Uri.EscapeDataString(name)}";

    public static ChordsKind ToKind(ContentKind kind) => Enum.Parse<ChordsKind>(kind.ToString());

    public static ContentKind ToContentKind(ChordsKind kind) => Enum.Parse<ContentKind>(kind.ToString());

    private void Remember(Caller caller, Uri? address, HeaderAnswer answer)
    {
        if (answer.State != ItemState.Unavailable)
            cache.Set(CacheKey(caller, answer.Id, address), answer, Keep);
    }

    private static (string, string, string, string, string?) CacheKey(Caller caller, string id, Uri? address)
        => ("chords-header", id, string.Join('\n', caller.Keys), string.Join('\n', caller.Subjects), address?.AbsoluteUri);
}
