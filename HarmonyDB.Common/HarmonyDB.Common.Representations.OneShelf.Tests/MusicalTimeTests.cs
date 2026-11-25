using System.Text.Json;
using System.Text.RegularExpressions;

namespace HarmonyDB.Common.Representations.OneShelf.Tests;

public class MusicalTimeTests
{
    // Two bars of 4/4 at 60 to the minute from 1 s, a bar of 2+2+3/8, a free bar; the sheet ends at 14 s.
    private static readonly SheetTimes Times = new()
    {
        End = 14,
        Bars =
        [
            new() { Beats = [1, 2, 3, 4], TimeSignature = "4/4" },
            new() { Beats = [5, 6, 7, 8] },
            new() { Beats = [9, 9.5, 10], TimeSignature = "2+2+3/8" },
            new() { Beats = [11, 13], TimeSignature = "free" },
        ],
    };

    private static NodeChord Chord(string text, SheetPlace? place = null, double? time = null)
        => new() { Children = { new NodeText(text).AsChild() }, Place = place, Time = time };

    private static NodeHtml Sheet(SheetTimes? times, params NodeBase[] nodes)
    {
        var sheet = new NodeHtml { Name = "pre", Times = times };
        sheet.Children.AddRange(nodes.Select(x => x.AsChild()));
        return sheet;
    }

    [Theory]
    [InlineData(1, 1, null, 1.0)]
    [InlineData(1, 3, null, 3.0)]
    [InlineData(1, 2, "1/2", 2.5)]
    [InlineData(1, 2, "2/4", 2.5)]
    [InlineData(1, 4, "1/4", 4.25)] // the last beat of a bar: towards the next bar's first
    [InlineData(3, 2, "1/2", 9.75)]
    [InlineData(3, 3, "1/2", 10.5)] // towards the next bar's first, 11
    [InlineData(4, 2, null, 13.0)]
    [InlineData(4, 2, "1/2", 13.5)] // the last beat of the sheet: towards the end
    [InlineData(4, 1, "0/3", 11.0)]
    public void Moments(int bar, int beat, string? at, double expected)
    {
        Assert.Equal(expected, Times.Moment(new(bar, beat, at)));
    }

    [Theory]
    [InlineData(5, 1)]
    [InlineData(1, 5)]
    [InlineData(4, 3)]
    public void PlacesOutsideTheBarsHaveNoMoment(int bar, int beat)
    {
        Assert.Null(Times.Moment(new(bar, beat)));

        // and the sheet still renders, the node with no time
        var html = Sheet(Times, Chord("Am", new(bar, beat)), new NodeText(" "), Chord("C", new(1, 1))).AsHtml(new());
        Assert.Contains("chord-data='[!0!]m'>", html);
        Assert.Contains("chord-data='[!3!]' data-time='1'>", html);
    }

    [Fact]
    public void APlaceWithoutBarsHasNoMoment()
    {
        var html = Sheet(null, Chord("Am", new(1, 1))).AsHtml(new());
        Assert.DoesNotContain("data-time", html);
    }

    /// <summary>
    /// A place that cannot be resolved gives its node no time; the sheet is read, rendered and written back as it came.
    /// </summary>
    [Theory]
    [InlineData(0, 1, null)]
    [InlineData(1, 0, null)]
    [InlineData(-2, 1, null)]
    [InlineData(1, 1, "1")]
    [InlineData(1, 1, "1/1")]
    [InlineData(1, 1, "3/2")]
    [InlineData(1, 1, "-1/2")]
    [InlineData(1, 1, "1/0")]
    [InlineData(1, 1, "x")]
    public void BadPlacesHaveNoTime(int bar, int beat, string? at)
    {
        var place = new SheetPlace(bar, beat, at);
        Assert.Null(Times.Moment(place));

        var sheet = Sheet(Times, Chord("Am", place), new NodeText(" "), new NodeMark { Meter = true, Place = place }, new NodeText(" "), Chord("C", new(1, 2)));
        var json = JsonSerializer.Serialize(sheet);
        var back = JsonSerializer.Deserialize<NodeHtml>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(back));
        Assert.Equal(place, back.Children[0].NodeChord!.Place);

        var html = back.AsHtml(new());
        Assert.Contains("chord-data='[!0!]m'>", html);
        Assert.Contains("chord-data='[!3!]' data-time='2'>", html);
        Assert.DoesNotContain("sheet-mark-time-signature", html);
    }

    [Fact]
    public void PlaceBesideTimeHasNoTime()
    {
        var html = Sheet(Times, Chord("Am", new(1, 1), 1), new NodeText(" "), new NodeMark { Key = new("C", false), Place = new(1, 1), Time = 1 }).AsHtml(new());
        Assert.DoesNotContain("data-time", html);
    }

    /// <summary>
    /// A sheet as it comes from elsewhere, with "+" and Cyrillic written as they are, read and written back with the options of the shelf's wire format
    /// (web defaults: camel case; the default encoder). Written back it means the same; the first writing escapes those characters, then it stays.
    /// </summary>
    [Fact]
    public void ForeignTextRoundTrip()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        const string json = """{"name":"pre","classes":null,"times":{"end":4,"bars":[{"beats":[0,1,2,3],"time_sig":"2+2+3/8"}]},"children":[{"nodeText":{"text":"Привет, мир +","time":0.5}}]}""";
        var sheet = JsonSerializer.Deserialize<NodeHtml>(json, options)!;
        var written = JsonSerializer.Serialize(sheet, options);
        Assert.NotEqual(json, written);
        Assert.Contains("\\u002B", written);
        Assert.Contains("\\u041F", written);
        var again = JsonSerializer.Deserialize<NodeHtml>(written, options)!;
        Assert.Equal("Привет, мир +", again.Children[0].NodeText!.Text);
        Assert.Equal("2+2+3/8", again.Times!.Bars[0].TimeSignature);
        Assert.Equal(written, JsonSerializer.Serialize(again, options));
    }

    [Fact]
    public void MixedSheet()
    {
        var sheet = Sheet(
            Times,
            new NodeMark { Key = new("A", true), Meter = true, Place = new(1, 1) },
            new NodeText("\n"),
            Chord("Am", new(1, 1)),
            new NodeText("    "),
            Chord("E7", new(2, 3, "1/2")),
            new NodeText("\n"),
            new NodeText("hello ") { Time = 1.1 },
            new NodeText("world") { Time = 7.4 });

        var html = sheet.AsHtml(new());
        Assert.Contains("<span class='sheet-mark' style='white-space: nowrap' data-time='1'>", html);
        Assert.Contains("chord-data='[!0!]m' data-time='1'>", html);
        Assert.Contains("data-time='7.5'>E", html);
        Assert.Contains("<span data-time='1.1'>hello </span>", html);
        Assert.Contains("<span data-time='7.4'>world</span>", html);

        // the line of the chords over the words takes the first moment in it
        Assert.Contains("<span class='chords-line chords-double-line' data-time='1'>", html);

        // transposed, the moments stay
        Assert.Equal(Regex.Matches(html, "data-time='[^']*'").Select(x => x.Value), Regex.Matches(sheet.AsHtml(new(5)), "data-time='[^']*'").Select(x => x.Value));
    }

    [Theory]
    [InlineData(1, "aria-label='4/4'", ">4</text>")]
    [InlineData(2, "aria-label='4/4'", ">4</text>")] // held from the bar before
    [InlineData(3, "aria-label='2+2+3/8'", ">2+2+3</text>")]
    [InlineData(4, "data-time-signature='free'", "<i>senza misura</i>")]
    public void MeterOfTheBar(int bar, string label, string drawn)
    {
        var html = Sheet(Times, new NodeMark { Meter = true, Place = new(bar, 1) }).AsHtml(new());
        Assert.Contains(label, html);
        Assert.Contains(drawn, html);
    }

    [Fact]
    public void MeterWithoutBarsShowsNothing()
    {
        var html = Sheet(null, new NodeMark { Meter = true, Key = new("C", false), Time = 2 }).AsHtml(new());
        Assert.DoesNotContain("sheet-mark-time-signature", html);
        Assert.Contains("data-key='C'", html);
    }

    [Fact]
    public void TextFormHasNoMarks()
    {
        var sheet = Sheet(Times, new NodeMark { Meter = true, Place = new(1, 1) }, new NodeText("\n"), Chord("Am", new(1, 1)), new NodeText("\nla la"));
        Assert.Equal(Sheet(null, Chord("Am"), new NodeText("\nla la")).AsOneShelf(new()), sheet.AsOneShelf(new()));
    }

    [Fact]
    public void RoundTrip()
    {
        var sheet = Sheet(
            Times,
            new NodeMark { Meter = true, Place = new(3, 1) },
            new NodeText("\n"),
            Chord("Am", new(1, 2, "1/2")),
            Chord("C", new(1, 3)),
            new NodeText("\n"),
            new NodeText("word") { Time = 1.5 });

        var json = JsonSerializer.Serialize(sheet);
        Assert.Contains("\"time_sig\":\"free\"", json);
        Assert.Contains("\"Place\":{\"Bar\":1,\"Beat\":2,\"At\":\"1/2\"}", json);
        Assert.Contains("\"Place\":{\"Bar\":1,\"Beat\":3}", json);
        Assert.Contains("\"Meter\":true", json);

        var back = JsonSerializer.Deserialize<NodeHtml>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(back));
        Assert.Equal("2+2+3/8", back.Times!.Bars[2].TimeSignature);
        Assert.Equal([9, 9.5, 10], back.Times.Bars[2].Beats);
        Assert.Equal(14, back.Times.End);
        Assert.Equal(sheet.AsHtml(new()), back.AsHtml(new()));

        // the names as the source contract writes them (camel case), "time_sig" as it is
        var camel = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var camelJson = JsonSerializer.Serialize(sheet, camel);
        Assert.Contains("\"times\":{\"end\":14,\"bars\":[{\"beats\":[1,2,3,4],\"time_sig\":\"4/4\"}", camelJson);
        Assert.Contains("\"place\":{\"bar\":1,\"beat\":2,\"at\":\"1/2\"}", camelJson);
        Assert.Contains("\"meter\":true", camelJson);
        Assert.Equal(camelJson, JsonSerializer.Serialize(JsonSerializer.Deserialize<NodeHtml>(camelJson, camel), camel));
    }

    [Fact]
    public void CloningAndSplittingKeepPlaces()
    {
        // the chords over the words: the layout clones the chords and moves them into the words' line
        var sheet = Sheet(Times, Chord("Am", new(2, 1)), new NodeText("\nla la"));
        Assert.Contains("data-time='5'>A", sheet.AsHtml(new()));

        sheet.Fix0_Compress();
        Assert.Equal(new SheetPlace(2, 1), sheet.Children[0].NodeChord!.Place);
    }
}
