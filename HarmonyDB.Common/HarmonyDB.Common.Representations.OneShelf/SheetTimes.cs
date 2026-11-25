using System.Text.Json.Serialization;

namespace HarmonyDB.Common.Representations.OneShelf;

/// <summary>
/// The sheet's own bars, when it has them: the chords and the marks may then stand at places in them (<see cref="SheetPlace"/>)
/// instead of at seconds.
/// </summary>
public record SheetTimes
{
    /// <summary>
    /// Where the last bar ends, in seconds.
    /// </summary>
    public required double End { get; init; }

    public required IReadOnlyList<SheetBar> Bars { get; init; }

    /// <summary>
    /// The moment of a place, in seconds: the time of its beat, and for a place inside the beat (<see cref="SheetPlace.At"/>)
    /// that part of the way to the next beat — the bar's next beat, else the next bar's first, else <see cref="End"/>:
    /// t(beat) + at × (t(next beat) − t(beat)).
    /// None (null) when the place cannot be resolved: the bar or the beat is below 1 or not in the bars, the fraction is not one from 0 up to 1
    /// (1 not included), or the next beat is not there to measure to.
    /// </summary>
    public double? Moment(SheetPlace place)
    {
        if (place.Fraction is not { } fraction) return null;
        var (numerator, denominator) = fraction;
        if (place.Bar < 1 || place.Bar > Bars.Count) return null;
        var beats = Bars[place.Bar - 1].Beats;
        if (place.Beat < 1 || place.Beat > beats.Count) return null;

        var time = beats[place.Beat - 1];
        if (numerator == 0) return time;

        double next;
        if (place.Beat < beats.Count) next = beats[place.Beat];
        else if (place.Bar < Bars.Count && Bars[place.Bar].Beats.Count > 0) next = Bars[place.Bar].Beats[0];
        else if (place.Bar == Bars.Count) next = End;
        else return null;

        return time + numerator * (next - time) / denominator;
    }

    /// <summary>
    /// The time signature in force in the bar (counted from 1): the one written on it or on the last bar before it that has one;
    /// none (null) when no bar up to it has one or the bar is not in the bars. "4/4", "2+2+3/8", "free".
    /// </summary>
    public string? TimeSignatureOf(int bar)
    {
        if (bar < 1 || bar > Bars.Count) return null;
        for (var i = bar - 1; i >= 0; i--)
            if (Bars[i].TimeSignature != null)
                return Bars[i].TimeSignature;

        return null;
    }
}

/// <summary>
/// A bar: its beats in seconds, the first where the bar begins; its time signature where it is said or changes, held until the next.
/// </summary>
public record SheetBar
{
    public required IReadOnlyList<double> Beats { get; init; }

    /// <summary>
    /// "4/4", "12/8", "2+2+3/8" (an additive one), "free" (a bar of no metre). Named in snake case in JSON whatever the naming of the rest:
    /// the bars are a structure shared with other formats as it is.
    /// </summary>
    [JsonPropertyName("time_sig")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TimeSignature { get; init; }
}

/// <summary>
/// A musical place in the sheet's bars (<see cref="SheetTimes"/>): a bar and a beat, counted from 1, and how far into the beat
/// as a fraction below 1 ("1/2", "3/4"; "2/4" is "1/2"), left out on the beat.
/// </summary>
public record SheetPlace
{
    /// <summary>
    /// Keeps what is given as it is, also a place that cannot be resolved (a bar or a beat below 1, an <paramref name="at"/> that is not a
    /// fraction below 1): such a place gives its node no time (<see cref="SheetTimes.Moment"/>), and the sheet is read and written back unchanged.
    /// </summary>
    [JsonConstructor]
    public SheetPlace(int bar, int beat, string? at = null)
    {
        Bar = bar;
        Beat = beat;
        At = at;
        Fraction = at == null ? (0, 1) : ParseFraction(at);
    }

    public int Bar { get; }

    public int Beat { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? At { get; }

    /// <summary>
    /// <see cref="At"/> read: 0 on the beat; none (null) when it is not a fraction from 0 up to 1, 1 not included.
    /// </summary>
    [JsonIgnore]
    internal (int numerator, int denominator)? Fraction { get; }

    private static (int, int)? ParseFraction(string at)
    {
        var parts = at.Split('/');
        return parts.Length == 2 && int.TryParse(parts[0], out var numerator) && int.TryParse(parts[1], out var denominator)
               && numerator >= 0 && denominator > 0 && numerator < denominator
            ? (numerator, denominator)
            : null;
    }
}
