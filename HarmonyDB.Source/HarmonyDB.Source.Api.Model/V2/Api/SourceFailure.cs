namespace HarmonyDB.Source.Api.Model.V2.Api;

/// <summary>
/// A source that did not answer an operation that asks every source (a search, the sources' labels).
/// </summary>
public record SourceFailure
{
    /// <summary>
    /// The name of the source as the one that asked it is configured with it.
    /// </summary>
    public required string Source { get; init; }

    public required SourceFailureReason Reason { get; init; }
}

public enum SourceFailureReason
{
    /// <summary>It did not answer in time.</summary>
    Timeout,

    /// <summary>It could not be reached, or answered with an error.</summary>
    Unavailable,
}