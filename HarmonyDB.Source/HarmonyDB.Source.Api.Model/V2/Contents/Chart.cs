namespace HarmonyDB.Source.Api.Model.V2.Contents;

/// <summary>
/// Chord charts of the material: those it has.
/// </summary>
public record Chart
{
    public BandChart? BandChart { get; init; }

    /// <summary>
    /// One song as iReal Pro writes it: one line, an <c>irealb://</c> or <c>irealbook://</c> address.
    /// </summary>
    public string? IReal { get; init; }
}

/// <summary>
/// A chord chart as a band reads it: the parts, their bars with chords, words for the players. Every pass of a repeat
/// is written out.
/// </summary>
public record BandChart
{
    /// <summary>
    /// The meter, as <see cref="Bar.TimeSignature"/>, when the chart states one.
    /// </summary>
    public string? TimeSignature { get; init; }

    public required IReadOnlyList<BandChartPart> Parts { get; init; }
}

public record BandChartPart
{
    /// <summary>
    /// As the chart writes it ("V1", "Ch").
    /// </summary>
    public required string Name { get; init; }

    public SectionLabel? Label { get; init; }

    public required IReadOnlyList<BandChartBar> Bars { get; init; }
}

public record BandChartBar
{
    public required double Start { get; init; }

    public required double End { get; init; }

    /// <summary>
    /// What begins in the bar, standing evenly in it; empty: what sounds goes on.
    /// </summary>
    public required IReadOnlyList<ChordSymbol> Chords { get; init; }

    /// <summary>
    /// A word for the players over the bar ("pads").
    /// </summary>
    public string? Note { get; init; }
}
