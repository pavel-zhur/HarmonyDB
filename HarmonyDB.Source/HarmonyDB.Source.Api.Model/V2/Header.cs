namespace HarmonyDB.Source.Api.Model.V2;

/// <summary>
/// What is known of an item without its content, as given to this caller.
/// </summary>
public record Header
{
    /// <summary>
    /// The item's id: opaque, stable, the same for every caller. Its prefix routes it to its source.
    /// </summary>
    public required string Id { get; init; }

    public required SourceLabel Source { get; init; }

    public string? Title { get; init; }

    public required IReadOnlyList<string> Artists { get; init; }

    /// <summary>
    /// The source's own rating of the item, when it has one.
    /// </summary>
    public float? Rating { get; init; }

    /// <summary>
    /// The source's own type of the item (as the source names it), when it has one.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// The kinds of content the item has.
    /// </summary>
    public required IReadOnlyList<ContentKind> Kinds { get; init; }

    public required HeaderAttributes Attributes { get; init; }

    /// <summary>
    /// Whether the item may be shown on public pages.
    /// </summary>
    public required bool IsPublic { get; init; }
}
