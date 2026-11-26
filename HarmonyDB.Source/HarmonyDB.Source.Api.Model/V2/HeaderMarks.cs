namespace HarmonyDB.Source.Api.Model.V2;

/// <summary>
/// What the source marks about the item, where it marks it.
/// </summary>
public record HeaderMarks
{
    /// <summary>
    /// The source vouches for the item as one of its best.
    /// </summary>
    public bool IsChosen { get; init; }

    /// <summary>
    /// The source's words for that mark, when it has them.
    /// </summary>
    public string? ChosenText { get; init; }

    /// <summary>
    /// Whether the source fully supports the item's kind; null when it does not say.
    /// </summary>
    public bool? IsSupported { get; init; }

    /// <summary>
    /// The source's short name, for printed lines.
    /// </summary>
    public string? ShortSource { get; init; }
}
