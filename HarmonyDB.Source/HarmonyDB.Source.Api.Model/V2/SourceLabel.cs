namespace HarmonyDB.Source.Api.Model.V2;

/// <summary>
/// A source as it is shown to this caller: of an item (where it comes from), or in the list of the sources the caller
/// may see (<see cref="SourceV2Urls.V2GetSources"/>). It says nothing of one item, and may differ between callers, so it
/// is never kept as a fact about an item.
/// </summary>
public record SourceLabel
{
    /// <summary>
    /// A stable key of the source as shown to this caller.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The name to show.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// An opaque name of the source's picture, fetched by <see cref="SourceV2Urls.V2GetIcon"/>.
    /// </summary>
    public string? Icon { get; init; }
}