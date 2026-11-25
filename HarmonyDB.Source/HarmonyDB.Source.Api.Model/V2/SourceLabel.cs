namespace HarmonyDB.Source.Api.Model.V2;

/// <summary>
/// Where an item comes from, exactly as the source gives it for this caller. It may differ between callers, so it is
/// never kept as a fact about the item.
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
    /// The address of this item at the source, when it is given to this caller.
    /// </summary>
    public Uri? Uri { get; init; }

    /// <summary>
    /// An opaque name of the source's picture, fetched by <see cref="SourceV2Urls.V2GetIcon"/>.
    /// </summary>
    public string? Icon { get; init; }
}
