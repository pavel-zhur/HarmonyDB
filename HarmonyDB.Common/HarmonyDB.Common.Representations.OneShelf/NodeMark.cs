using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;

namespace HarmonyDB.Common.Representations.OneShelf;

/// <summary>
/// A mark in the flow of the text, on a line of its own: here the key, the tempo or the time signature changes (or is set, at the start).
/// Rendered as a musician writes them: the key signature on a piece of staff with the key's name, the time signature, the tempo as a note and its number.
/// The text and the chords know nothing of these marks: a sheet without them is read as before.
/// </summary>
public class NodeMark : NodeBase
{
    // The drawings are in staff spaces of 10 units; the staff's top line is at 0, its bottom line at 40.
    private const int Space = 10;

    // The places of the signs of a key signature on the treble staff, in half spaces down from the top line.
    private static readonly int[] SharpSteps = [0, 3, -1, 2, 5, 1, 4];
    private static readonly int[] FlatSteps = [4, 1, 5, 2, 6, 3, 7];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SheetKey? Key { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SheetTempo? Tempo { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SheetTimeSignature? TimeSignature { get; init; }

    /// <summary>
    /// When the change happens, in seconds from the start, when known.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Time { get; init; }

    /// <summary>
    /// Where the change happens in the sheet's bars (<see cref="NodeHtml.Times"/>), in place of <see cref="Time"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SheetPlace? Place { get; init; }

    /// <summary>
    /// In a sheet with bars (<see cref="NodeHtml.Times"/>): show here the time signature that the bar of the mark's <see cref="Place"/> has there,
    /// in place of a <see cref="TimeSignature"/> of the mark's own. Without bars, or with a place that does not resolve (<see cref="SheetTimes.Moment"/>), it shows nothing.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Meter { get; init; }

    internal override double? Moment(SheetTimes? times) => Moment(Time, Place, times);

    internal override void AppendHtmlTo(StringBuilder builder, RepresentationSettings representationSettings, TraversalContext traversalContext)
    {
        if (Key == null && Tempo == null && TimeSignature == null && !Meter)
            throw new InvalidOperationException("A mark carries a key, a tempo, a time signature or the meter.");

        builder.Append("<span class='sheet-mark' style='white-space: nowrap'");
        if (Moment(traversalContext.Times) is { } moment)
        {
            builder.Append(' ');
            builder.Append(TimeAttribute.Format(moment));
        }

        builder.Append('>');

        var parts = new List<string>();
        if (Key != null) parts.Add(KeyHtml(Key.Shown(representationSettings)));
        if (TimeSignature != null) parts.Add(TimeSignatureHtml(TimeSignature.Beats.ToString(), TimeSignature.Unit.ToString()));
        // The meter of the bar of the mark's place, when the place resolves.
        if (Meter && Place != null && Moment(traversalContext.Times) != null && traversalContext.Times!.TimeSignatureOf(Place.Bar) is { } meter)
            parts.Add(MeterHtml(meter));
        if (Tempo != null) parts.Add(TempoHtml(Tempo));
        builder.Append(string.Join(" ", parts));

        builder.Append("</span>");
    }

    private static string N(double x) => x.ToString(CultureInfo.InvariantCulture);

    private static string Svg(double left, double top, double width, double height, double heightEm, string content)
        => $"<svg viewBox='{N(left)} {N(top)} {N(width)} {N(height)}' height='{N(heightEm)}em' width='{N(Math.Round(heightEm * width / height, 3))}em' style='vertical-align: middle' fill='currentColor' stroke='currentColor' aria-hidden='true'>{content}</svg>";

    private static string KeyHtml(SheetKey key)
    {
        var name = HttpUtility.HtmlEncode(key.Name);
        var start = $"<span class='sheet-mark-key' data-key='{HttpUtility.HtmlAttributeEncode(key.Name)}'>";

        // A key of more than seven sharps or flats, as it is named, has no signature: its name alone.
        if (!key.HasSignature)
            return $"{start}<span class='sheet-mark-key-name'>{name}</span></span>";

        var signature = key.Signature;
        var steps = signature >= 0 ? SharpSteps : FlatSteps;
        var count = Math.Abs(signature);
        var width = Space + count * Space + Space;

        var content = new StringBuilder();
        for (var line = 0; line < 5; line++)
            content.Append($"<line x1='0' x2='{width}' y1='{line * Space}' y2='{line * Space}' stroke-width='1' opacity='0.6'/>");

        for (var i = 0; i < count; i++)
        {
            double x = Space + i * Space + Space / 2.0, y = steps[i] * Space / 2.0;
            content.Append(signature > 0 ? SharpSign(x, y) : FlatSign(x, y));
        }

        // The staff is as tall as a line of text and a half; the signs stand out of it above and below.
        return $"{start}{Svg(0, -18, width, 62, 2.1, content.ToString())} <span class='sheet-mark-key-name'>{name}</span></span>";
    }

    private static string SharpSign(double x, double y)
    {
        string Bar(double c) => $"<polygon stroke='none' points='{N(x - 5)},{N(c + 1.5)} {N(x + 5)},{N(c - 1.5)} {N(x + 5)},{N(c + 1.5)} {N(x - 5)},{N(c + 4.5)}'/>";
        return $"<line x1='{N(x - 2)}' x2='{N(x - 2)}' y1='{N(y - 9)}' y2='{N(y + 12)}' stroke-width='1.3'/>"
               + $"<line x1='{N(x + 2)}' x2='{N(x + 2)}' y1='{N(y - 12)}' y2='{N(y + 9)}' stroke-width='1.3'/>"
               + Bar(y - 6) + Bar(y + 1);
    }

    private static string FlatSign(double x, double y)
        => $"<line x1='{N(x - 3)}' x2='{N(x - 3)}' y1='{N(y - 16)}' y2='{N(y + 5)}' stroke-width='1.4'/>"
           + $"<path fill='none' stroke-width='1.8' d='M {N(x - 3)} {N(y + 5)} C {N(x + 1)} {N(y + 2)} {N(x + 6)} {N(y - 2)} {N(x + 3)} {N(y - 4)} C {N(x + 1)} {N(y - 5.5)} {N(x - 1)} {N(y - 3)} {N(x - 3)} {N(y - 1)}'/>";

    /// <summary>
    /// A time signature as a score writes it: the beats over the note, "3" over "4", "2+2+3" over "8".
    /// </summary>
    private static string TimeSignatureHtml(string beats, string unit)
    {
        var characters = Math.Max(beats.Length, unit.Length);
        var width = 13 * characters + 2;
        var content = $"<text stroke='none' x='{N(width / 2.0)}' y='18' text-anchor='middle' font-size='22' font-weight='bold' font-family='serif'>{HttpUtility.HtmlEncode(beats)}</text>"
                      + $"<text stroke='none' x='{N(width / 2.0)}' y='39' text-anchor='middle' font-size='22' font-weight='bold' font-family='serif'>{HttpUtility.HtmlEncode(unit)}</text>";
        return $"<span class='sheet-mark-time-signature' role='img' aria-label='{HttpUtility.HtmlAttributeEncode($"{beats}/{unit}")}'>{Svg(0, 0, width, 40, 1.4, content)}</span>";
    }

    /// <summary>
    /// The time signature of a bar of the sheet's bars: a plain one ("3/4") and an additive one ("2+2+3/8") drawn as a time signature is;
    /// a bar of no metre ("free") as a score says it, "senza misura"; anything else written as it is.
    /// </summary>
    private static string MeterHtml(string meter)
    {
        var parts = meter.Split('/');
        if (parts.Length == 2 && Regex.IsMatch(parts[0], @"^\d+(\+\d+)*$") && Regex.IsMatch(parts[1], @"^\d+$"))
            return TimeSignatureHtml(parts[0], parts[1]);

        var text = meter == "free" ? "senza misura" : meter;
        return $"<span class='sheet-mark-time-signature' data-time-signature='{HttpUtility.HtmlAttributeEncode(meter)}'><i>{HttpUtility.HtmlEncode(text)}</i></span>";
    }

    private static string TempoHtml(SheetTempo tempo)
    {
        var flags = tempo.Unit switch { 8 => 1, 16 => 2, _ => 0 };
        var width = 13 + (flags > 0 ? 6 : 0) + (tempo.Dotted ? 6 : 0);

        var content = new StringBuilder();
        var hollow = tempo.Unit <= 2;
        content.Append($"<ellipse cx='6' cy='32' rx='5' ry='3.6' transform='rotate(-20 6 32)' {(hollow ? "fill='none' stroke-width='1.6'" : "stroke='none'")}/>");
        if (tempo.Unit >= 2)
            content.Append("<line x1='10.6' x2='10.6' y1='31' y2='4' stroke-width='1.4'/>");

        for (var i = 0; i < flags; i++)
            content.Append($"<path fill='none' stroke-width='1.6' d='M 10.6 {4 + i * 6} C 12 {10 + i * 6} 18 {12 + i * 6} 16 {20 + i * 6}'/>");

        if (tempo.Dotted)
            content.Append($"<circle stroke='none' cx='{width - 3}' cy='32' r='1.7'/>");

        var bpm = Math.Round(tempo.BeatsPerMinute).ToString(CultureInfo.InvariantCulture);
        return $"<span class='sheet-mark-tempo' data-bpm='{N(tempo.BeatsPerMinute)}'>{Svg(0, 0, width, 40, 1.3, content.ToString())} = {bpm}</span>";
    }

    internal override void AppendChordsTo(List<string> destination, RepresentationSettings representationSettings)
    {
    }

    internal override void AppendLyricsTo(StringBuilder builder)
    {
    }

    internal override void AppendHtmlAttributeTo(StringBuilder builder, RepresentationSettings representationSettings)
    {
    }

    // The text form of the sheet has no marks, and leaves no trace of them: the space after a mark that stands between two words is dropped
    // (and a line of marks alone, see NodeHtml.AsOneShelf).
    internal override void AppendOneShelfTo(StringBuilder builder, RepresentationSettings representationSettings,
        bool isBold, ref bool owesWhitespace, bool alreadyChordLine)
    {
        if (builder.Length > 0 && builder[^1] == ' ')
            owesWhitespace = true;
    }

    public override NodeBase ShallowClone() => new NodeMark
    {
        Key = Key,
        Tempo = Tempo,
        TimeSignature = TimeSignature,
        Time = Time,
        Place = Place,
        Meter = Meter,
    };
}
