namespace HarmonyDB.Source.Api.Model.V2.Api;

public record GetHeadersRequest
{
    public required Caller Caller { get; init; }

    public required IReadOnlyList<string> Ids { get; init; }
}

public record GetHeadersResponse
{
    /// <summary>
    /// One answer for every id asked, in the order asked.
    /// </summary>
    public required IReadOnlyList<HeaderAnswer> Headers { get; init; }
}

/// <summary>
/// The answer for one id: its state, and its header when it is open.
/// </summary>
public record HeaderAnswer
{
    public required string Id { get; init; }

    public required ItemState State { get; init; }

    /// <summary>
    /// The header, present exactly when <see cref="State"/> is <see cref="ItemState.Open"/>.
    /// </summary>
    public Header? Header { get; init; }
}
