namespace HarmonyDB.Source.Api.Model.V2.Api;

/// <summary>
/// The sources this caller may see.
/// </summary>
public record GetSourcesRequest
{
    public required Caller Caller { get; init; }
}

public record GetSourcesResponse
{
    /// <summary>
    /// The labels the source's items carry for this caller, each once: exactly those a search by this caller could show.
    /// Through the index: every source's, in the order of its sources.
    /// </summary>
    public required IReadOnlyList<SourceLabel> Sources { get; init; }

    /// <summary>
    /// The sources that were asked and did not answer: their labels are missing. Empty when every source answered.
    /// </summary>
    public required IReadOnlyList<SourceFailure> Failures { get; init; }
}