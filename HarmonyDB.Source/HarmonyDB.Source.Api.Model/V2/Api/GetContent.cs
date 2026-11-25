namespace HarmonyDB.Source.Api.Model.V2.Api;

public record GetContentRequest
{
    public required Caller Caller { get; init; }

    public required string Id { get; init; }

    /// <summary>
    /// The kinds wanted. Kinds the item does not have are left out of the answer.
    /// </summary>
    public required IReadOnlyList<ContentKind> Kinds { get; init; }
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
