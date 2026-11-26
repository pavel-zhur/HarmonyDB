namespace HarmonyDB.Source.Api.Model.V2.Api;

/// <summary>
/// Addresses of items (pages of the sources) to their ids.
/// </summary>
public record ResolveRequest
{
    public required Caller Caller { get; init; }

    public required IReadOnlyList<Uri> Uris { get; init; }

    /// <summary>
    /// Whether the request is one the contract takes: a caller, and addresses, each absolute.
    /// </summary>
    public static bool IsValid(ResolveRequest request)
        => Caller.IsValid(request.Caller) && request.Uris != null && request.Uris.All(x => x is { IsAbsoluteUri: true });
}

public record ResolveResponse
{
    /// <summary>
    /// One answer for every address asked, in the order asked.
    /// </summary>
    public required IReadOnlyList<ResolvedUri> Items { get; init; }
}

public record ResolvedUri
{
    public required Uri Uri { get; init; }

    public required ResolveState State { get; init; }

    /// <summary>
    /// The item's id, present exactly when <see cref="State"/> is <see cref="ResolveState.Resolved"/>.
    /// </summary>
    public string? Id { get; init; }
}

public enum ResolveState
{
    /// <summary>A source knows the address, and the item is open to the caller.</summary>
    Resolved,

    /// <summary>No source knows the address, or the item is not open to the caller.</summary>
    NotResolved,

    /// <summary>A source that may know the address did not answer.</summary>
    Unavailable,
}
