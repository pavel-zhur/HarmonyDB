namespace HarmonyDB.Source.Api.Model.V2;

/// <summary>
/// What the source states about the item, where it states it.
/// </summary>
public record HeaderAttributes
{
    /// <summary>
    /// The key the source names for the item: a note and an optional "m" ("C", "F#m").
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Beats a minute.
    /// </summary>
    public double? Bpm { get; init; }

    /// <summary>
    /// The fret of the capo the item is written for.
    /// </summary>
    public int? Capo { get; init; }

    /// <summary>
    /// The meter, as "4/4", "6/8".
    /// </summary>
    public string? TimeSignature { get; init; }
}
