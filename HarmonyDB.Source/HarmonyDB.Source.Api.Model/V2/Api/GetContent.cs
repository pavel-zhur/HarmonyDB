namespace HarmonyDB.Source.Api.Model.V2.Api;

public record GetContentRequest
{
    public required Caller Caller { get; init; }

    public required string Id { get; init; }

    /// <summary>
    /// The kinds wanted. Kinds the item does not have are left out of the answer.
    /// </summary>
    public required IReadOnlyList<ContentKind> Kinds { get; init; }

    /// <summary>
    /// The address the person came by (a link opened, or the <see cref="Header.Address"/> of a search's result), when
    /// they came by one; null for a read by the id. A source may open an item to whoever holds its address, by its own
    /// rule; an address opens only the item it is the address of.
    /// </summary>
    public Uri? Address { get; init; }

    /// <summary>
    /// Whether the request is one the contract takes: a caller, an id, kinds, and an address only an absolute one.
    /// </summary>
    public static bool IsValid(GetContentRequest request)
        => Caller.IsValid(request.Caller) && !string.IsNullOrEmpty(request.Id) && request.Kinds != null
           && request.Address is null or { IsAbsoluteUri: true };
}

/// <summary>
/// The item's state, and its header and content when it is open.
/// </summary>
public record GetContentResponse
{
    public required string Id { get; init; }

    public required ItemState State { get; init; }

    /// <summary>
    /// Present exactly when <see cref="State"/> is <see cref="ItemState.Open"/>.
    /// </summary>
    public Header? Header { get; init; }

    /// <summary>
    /// The kinds asked that the item has; present exactly when <see cref="State"/> is <see cref="ItemState.Open"/>.
    /// </summary>
    public Content? Content { get; init; }
}
