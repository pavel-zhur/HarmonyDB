namespace HarmonyDB.Source.Api.Model.V2.Api;

public record SearchRequest
{
    public required Caller Caller { get; init; }

    public required string Query { get; init; }
}

public record SearchResponse
{
    /// <summary>
    /// What was found, open to the caller. A search may find an item by something only those it was given to know; the
    /// header of such a result carries in <see cref="Header.Address"/> the address that opens it.
    /// </summary>
    public required IReadOnlyList<Header> Headers { get; init; }

    /// <summary>
    /// The sources that were asked and did not answer: their results are missing, which is not "nothing found".
    /// Empty when every source answered.
    /// </summary>
    public required IReadOnlyList<SourceFailure> Failures { get; init; }
}
