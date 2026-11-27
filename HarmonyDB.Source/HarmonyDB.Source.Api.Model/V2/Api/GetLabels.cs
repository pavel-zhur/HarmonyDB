namespace HarmonyDB.Source.Api.Model.V2.Api;

/// <summary>
/// The labels of many items at once: for each, its source as this caller is shown it. A cheap question: a source
/// answers it from the ids alone, reading no item.
/// </summary>
public record GetLabelsRequest
{
    /// <summary>
    /// The most ids one call may ask for.
    /// </summary>
    public const int MaxIds = 1000;

    public required Caller Caller { get; init; }

    /// <summary>
    /// The items, each once; at most <see cref="MaxIds"/>.
    /// </summary>
    public required IReadOnlyList<string> Ids { get; init; }

    /// <summary>
    /// Whether the request is one the contract takes: a caller, and ids — each once, not empty, at most
    /// <see cref="MaxIds"/>.
    /// </summary>
    public static bool IsValid(GetLabelsRequest request)
        => Caller.IsValid(request.Caller)
           && request.Ids is { Count: <= MaxIds } && request.Ids.All(x => !string.IsNullOrEmpty(x))
           && request.Ids.Distinct(StringComparer.Ordinal).Count() == request.Ids.Count;
}

public record GetLabelsResponse
{
    /// <summary>
    /// The label of every id asked that the source knows, in the order asked; an id it does not know is left out.
    /// </summary>
    public required IReadOnlyList<ItemLabel> Labels { get; init; }
}

public record ItemLabel
{
    public required string Id { get; init; }

    /// <summary>
    /// The item's source as this caller is shown it, as far as the id alone tells it.
    /// </summary>
    public required SourceLabel Source { get; init; }
}
