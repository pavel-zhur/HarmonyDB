namespace HarmonyDB.Source.Api.Model.V2.Api;

/// <summary>
/// The content of several items in one call: for each, the answer <see cref="GetContentRequest"/> would give for it.
/// </summary>
public record GetContentsRequest
{
    /// <summary>
    /// The most ids one call may ask for.
    /// </summary>
    public const int MaxIds = 50;

    public required Caller Caller { get; init; }

    /// <summary>
    /// The items, each once; at most <see cref="MaxIds"/>.
    /// </summary>
    public required IReadOnlyList<string> Ids { get; init; }

    /// <summary>
    /// The kinds wanted of every item. Kinds an item does not have are left out of its answer.
    /// </summary>
    public required IReadOnlyList<ContentKind> Kinds { get; init; }

    /// <summary>
    /// Whether the request is one the contract takes: a caller, ids — each once, not empty, at most
    /// <see cref="MaxIds"/> — and kinds.
    /// </summary>
    public static bool IsValid(GetContentsRequest request)
        => request.Caller is { Keys: not null } && request.Caller.Keys.All(x => x != null)
           && request.Ids is { Count: <= MaxIds } && request.Ids.All(x => !string.IsNullOrEmpty(x))
           && request.Ids.Distinct(StringComparer.Ordinal).Count() == request.Ids.Count
           && request.Kinds != null;
}

public record GetContentsResponse
{
    /// <summary>
    /// One answer for every id asked, in the order asked, each as <see cref="GetContentResponse"/> is for one: an item
    /// that could not be had now is <see cref="ItemState.Unavailable"/> alone; the others are answered all the same.
    /// </summary>
    public required IReadOnlyList<GetContentResponse> Items { get; init; }
}
