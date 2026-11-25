namespace HarmonyDB.Source.Api.Model.V2.Contents;

/// <summary>
/// A score, and the times of its bars on the recording. Without times it is drawn, not played along.
/// </summary>
public record Score
{
    /// <summary>
    /// The score, MusicXML (score-partwise or score-timewise).
    /// </summary>
    public required string MusicXml { get; init; }

    public ScoreTimes? Times { get; init; }
}

/// <summary>
/// Bar i is the score's measure i in every part, a pickup measure a bar like any other. Within a bar time goes evenly
/// between two beats.
/// </summary>
public record ScoreTimes
{
    /// <summary>
    /// Where the last bar ends, seconds.
    /// </summary>
    public required double End { get; init; }

    public required IReadOnlyList<ScoreBar> Bars { get; init; }
}

public record ScoreBar
{
    /// <summary>
    /// The times of the bar's beats, as <see cref="Bar.Beats"/>.
    /// </summary>
    public required IReadOnlyList<double> Beats { get; init; }

    /// <summary>
    /// The meter in force in this bar, as <see cref="Bar.TimeSignature"/>.
    /// </summary>
    public required string TimeSignature { get; init; }
}
