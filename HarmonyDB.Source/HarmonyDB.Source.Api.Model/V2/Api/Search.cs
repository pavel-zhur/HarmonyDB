namespace HarmonyDB.Source.Api.Model.V2.Api;

public record SearchRequest
{
    public required Caller Caller { get; init; }

    public required string Query { get; init; }
}

public record SearchResponse
{
    /// <summary>
    /// What was found, open to the caller.
    /// </summary>
    public required IReadOnlyList<Header> Headers { get; init; }

    /// <summary>
    /// The sources that were asked and did not answer: their results are missing, which is not "nothing found".
    /// Empty when every source answered.
    /// </summary>
    public required IReadOnlyList<SearchFailure> Failures { get; init; }
}

/// <summary>
/// A source that did not answer a search.
/// </summary>
public record SearchFailure
{
    /// <summary>
    /// The name of the source as the one that asked it is configured with it.
    /// </summary>
    public required string Source { get; init; }

    public required SearchFailureReason Reason { get; init; }
}

public enum SearchFailureReason
{
    /// <summary>It did not answer in time.</summary>
    Timeout,

    /// <summary>It could not be reached, or answered with an error.</summary>
    Unavailable,
}
