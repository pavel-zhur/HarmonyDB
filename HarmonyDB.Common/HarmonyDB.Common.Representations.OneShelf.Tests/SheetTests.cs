using System.Text.Json;
using System.Text.RegularExpressions;

namespace HarmonyDB.Common.Representations.OneShelf.Tests;

public class SheetTests
{
    private static NodeChord Chord(string text, string? degree = null, string? bassDegree = null, double? time = null)
        => new() { Children = { new NodeText(text).AsChild() }, Degree = degree, BassDegree = bassDegree, Time = time };

    private static NodeHtml Sheet(params NodeBase[] nodes)
    {
        var sheet = new NodeHtml { Name = "pre" };
        sheet.Children.AddRange(nodes.Select(x => x.AsChild()));
        return sheet;
    }

    private static string Visible(string html) => System.Net.WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", string.Empty));

    [Fact]
    public void PlainSheetRendersAsBefore()
    {
        var sheet = Sheet(Chord("Am7"), new NodeText("\nla la\n"), Chord("C"));
        Assert.Equal(
            "<pre><div class='chords-double-line'><span class='chords-line chords-double-line'><span class='flying'><span class='flying2'><b class='chord' chord-index='0' chord-custom='' chord-data='[!0!]m7'>Am<sub>7</sub></b></span></span>la la</span></div><span class='chords-line '><b class='chord' chord-index='1' chord-custom='' chord-data='[!3!]'>C</b></span>\n</pre>",
            sheet.AsHtml(new()));
    }

    [Fact]
    public void OldJsonReadsAndWritesTheSame()
    {
        const string json = """{"Name":"pre","Classes":null,"Children":[{"NodeChord":{"Children":[{"NodeText":{"Text":"Am"}}]}},{"NodeText":{"Text":" la"}}]}""";
        var sheet = JsonSerializer.Deserialize<NodeHtml>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(sheet));
    }

    [Fact]
    public void NewDataRoundTrips()
    {
        var sheet = new NodeHtml
        {
            Name = "pre",
            Keys = [new("F#", true), new("A", false)],
            Children =
            {
                new NodeMark { Key = new("F#", true), Tempo = new(116), TimeSignature = new(4, 4), Time = 0 }.AsChild(),
                new NodeText("\n").AsChild(),
                Chord("C#7/E#", "V", "#VII", 1.5).AsChild(),
                new NodeText("\n").AsChild(),
                new NodeText("word") { Time = 1.52 }.AsChild(),
            },
        };

        var json = JsonSerializer.Serialize(sheet);
        var back = JsonSerializer.Deserialize<NodeHtml>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(back));
        Assert.Equal(sheet.AsHtml(new()), back.AsHtml(new()));
        Assert.Equal(["F#m", "A"], back.Keys!.Select(x => x.Name));
    }

    [Fact]
    public void Degrees()
    {
        var sheet = Sheet(Chord("Am7/G", "VI", "V"));
        Assert.Contains("<sup class='chord-degree'>VI/V</sup>", sheet.AsHtml(new()));
        Assert.Contains("<sup class='chord-degree'>VI</sup>", sheet.AsHtml(new() { Simplification = SimplificationMode.RemoveBass }));
        Assert.DoesNotContain("chord-degree", sheet.AsHtml(new() { ShowDegrees = false }));

        // Transposed, the degrees stay.
        Assert.Contains("<sup class='chord-degree'>VI/V</sup>", sheet.AsHtml(new(transpose: 3)));
        Assert.DoesNotContain("chord-degree", Sheet(Chord("Am")).AsHtml(new()));
    }

    [Fact]
    public void Superscripts()
    {
        var sheet = Sheet(Chord("C7b9"), new NodeText(" "), Chord("Dm7b5"));
        var html = sheet.AsHtml(new());
        Assert.Contains("C<sub>7</sub><sup>b9</sup>", html);
        var plain = sheet.AsHtml(new() { ChordSuperscripts = false });
        Assert.DoesNotContain("<sup>", plain);
        Assert.DoesNotContain("<sub>", plain);
        Assert.Equal("C7b9 Dm7b5\n", Visible(plain));
        Assert.Equal(Visible(html), Visible(plain));
    }

    [Theory]
    // shown as written
    [InlineData("E#m", 0, true, "E#m")]
    [InlineData("Cb", 0, true, "Cb")]
    [InlineData("Am/Fb", 0, true, "Am/Fb")]
    [InlineData("D#/F##", 0, true, "D#/F##")]
    [InlineData("Bbb7", 0, true, "Bbb7")]
    [InlineData("C#", 0, true, "C#")]
    [InlineData("Db", 0, true, "Db")]
    // named by the pitch, as always
    [InlineData("E#m", 0, false, "Fm")]
    [InlineData("Am/Fb", 0, false, "Am/E")]
    [InlineData("D#/F##", 0, false, "D#/F##")]
    [InlineData("F##m", 0, false, "F##m")]
    [InlineData("Bbb7", 0, false, "Bbb7")]
    [InlineData("Abba", 0, false, "Abba")]
    [InlineData("C#", 0, false, "C#")]
    [InlineData("Db", 0, false, "Db")]
    // transposed
    [InlineData("E#m", 1, true, "F#m")]
    [InlineData("D#/F##", 1, true, "E/G#")]
    [InlineData("F##m", 1, false, "G#m")]
    [InlineData("Bbb7", 2, false, "B7")]
    [InlineData("C##dim", -2, false, "Cdim")]
    // the last of two signs before 5, 9, 11 or 13 alters that degree
    [InlineData("Bbb5", 0, true, "Bbb5")]
    [InlineData("Bbb5", 1, true, "Bb5")]
    [InlineData("Abb9", 1, true, "Ab9")]
    [InlineData("Bbb13", 2, true, "Cb13")]
    [InlineData("C##5", 1, true, "D#5")]
    [InlineData("F##9", 1, true, "G#9")]
    public void SpelledNotes(string chord, int transpose, bool asWritten, string expected)
    {
        Assert.Equal(expected + "\n", Visible(Sheet(Chord(chord)).AsHtml(new(transpose) { ChordSuperscripts = false, ShowNotesAsWritten = asWritten })));
    }

    [Theory]
    [InlineData("Bbb5", "[!1b!]b5")]
    [InlineData("Abb9", "[!11b!]b9")]
    [InlineData("C##5", "[!4#!]#5")]
    [InlineData("F##9", "[!9#!]#9")]
    [InlineData("F##m", "[!10#!]m")]
    [InlineData("Bbb7", "[!0b!]7")]
    public void DoubleSigns(string chord, string data)
    {
        Assert.Equal([data], Sheet(Chord(chord)).AsChords(new()));
    }

    [Theory]
    [InlineData("Abb9", "Ab")]
    [InlineData("Bbb13", "Bb")]
    [InlineData("F##9", "F#")]
    [InlineData("Bbb7", "Bbb")]
    public void DoubleSignsSimplified(string chord, string expected)
    {
        Assert.Equal(expected, Visible(Sheet(Chord(chord)).AsHtml(new(simplification: SimplificationMode.All) { ShowNotesAsWritten = true })).Trim());
    }

    [Fact]
    public void Times()
    {
        var sheet = Sheet(Chord("Am", time: 1.25), new NodeText("\n"), new NodeText("hello ") { Time = 1.3 }, new NodeText("world") { Time = 2 });
        var html = sheet.AsHtml(new());
        Assert.Contains("chord-data='[!0!]m' data-time='1.25'>", html);
        Assert.Contains("<span data-time='1.3'>hello </span>", html);
        Assert.Contains("<span data-time='2'>world</span>", html);
        Assert.Contains("<span class='chords-line chords-double-line' data-time='1.25'>", html);
    }

    [Fact]
    public void CompressKeepsTimedWords()
    {
        var sheet = Sheet(new NodeText("a "), new NodeText("b") { Time = 1 }, new NodeText(" c"), new NodeText("d") { Time = 2 });
        sheet.Fix0_Compress();
        Assert.Equal([("a ", (double?)null), ("b c", 1), ("d", 2)], sheet.Children.Select(x => (x.NodeText!.Text, x.NodeText.Time)));

        var plain = Sheet(new NodeText("a "), new NodeText("b"));
        plain.Fix0_Compress();
        Assert.Equal("a b", Assert.Single(plain.Children).NodeText!.Text);
    }

    [Fact]
    public void Marks()
    {
        var sheet = Sheet(
            new NodeMark { Key = new("A", true), TimeSignature = new(3, 4), Tempo = new(72), Time = 0 },
            new NodeText("\n"),
            Chord("Am"),
            new NodeText("\nla la\n"),
            new NodeMark { Key = new("C", false), Time = 10 },
            new NodeText("\n"),
            Chord("C"),
            new NodeText("\nla la"));

        var html = sheet.AsHtml(new());
        Assert.Contains("<span class='sheet-mark' style='white-space: nowrap' data-time='0'>", html);
        Assert.Contains("data-key='Am'", html);
        Assert.Contains("aria-label='3/4'", html);
        Assert.Contains(" = 72</span>", html);
        Assert.Contains("data-key='C'", html);

        // The chords still stand over their words.
        Assert.Equal(2, Regex.Matches(html, "<div class='chords-double-line'>").Count);
        Assert.Equal("la la\nla la", sheet.AsLyrics());

        // Transposed, the marks' keys move with the chords.
        var moved = sheet.AsHtml(new(transpose: 1, alteration: NoteAlteration.Flat));
        Assert.Contains("data-key='Bbm'", moved);
        Assert.Contains("data-key='Db'", moved);
    }

    [Fact]
    public void MarksLeaveNoTraceInText()
    {
        var sheet = Sheet(
            new NodeMark { Key = new("A", true) },
            new NodeText("\n"),
            Chord("Am"),
            new NodeText("\nla "),
            new NodeMark { Tempo = new(90) },
            new NodeText(" la\n"),
            new NodeMark { TimeSignature = new(3, 4) },
            new NodeText("\nend"));

        var withoutMarks = Sheet(Chord("Am"), new NodeText("\nla la\nend"));
        Assert.Equal(withoutMarks.AsOneShelf(new()), sheet.AsOneShelf(new()));
    }

    /// <summary>
    /// The key of a mark is named as a chord on its tonic is, under every transposition and choice of sharps or flats.
    /// </summary>
    [Theory]
    [InlineData("Em")]
    [InlineData("C")]
    [InlineData("Bb")]
    [InlineData("F#m")]
    [InlineData("Cb")]
    [InlineData("E#m")]
    [InlineData("G#")]
    public void KeysAgreeWithChords(string key)
    {
        var sheet = Sheet(new NodeMark { Key = SheetKey.Parse(key) }, new NodeText("\n"), Chord(key));
        foreach (var asWritten in new[] { false, true })
        foreach (var alteration in new NoteAlteration?[] { null, NoteAlteration.Flat, NoteAlteration.Sharp })
        for (var transpose = -5; transpose <= 6; transpose++)
        {
            var settings = new RepresentationSettings(transpose, alteration) { ShowNotesAsWritten = asWritten, ChordSuperscripts = false };
            var html = sheet.AsHtml(settings);
            var chord = Regex.Match(html, "<b class='chord'[^>]*>([^<]*)</b>").Groups[1].Value;
            Assert.Contains($"data-key='{chord}'", html);
        }
    }

    /// <summary>
    /// A key is drawn with its signature only when it has at most seven signs, as it is named; otherwise its name stands alone.
    /// </summary>
    [Theory]
    [InlineData("G#", null, 8)]
    [InlineData("D#", null, 9)]
    [InlineData("A#", null, 10)]
    [InlineData("Fb", null, -8)]
    [InlineData("Dbm", null, -8)]
    [InlineData("F##", null, 13)]
    [InlineData("F#m", NoteAlteration.Flat, -9)]
    [InlineData("C#", null, 7)]
    [InlineData("Cb", null, -7)]
    [InlineData("F#m", null, 3)]
    public void KeySignatureDrawnOnlyWhenItExists(string key, NoteAlteration? alteration, int signature)
    {
        var html = Sheet(new NodeMark { Key = SheetKey.Parse(key) }).AsHtml(new(alteration: alteration) { ShowNotesAsWritten = true });
        var shown = Regex.Match(html, "data-key='([^']+)'").Groups[1].Value;
        Assert.Equal(signature, SheetKey.Parse(shown).Signature);
        Assert.Equal(Math.Abs(signature) <= 7, html.Contains("<svg"));
        Assert.Contains($"<span class='sheet-mark-key-name'>{shown}</span>", html);
    }

    [Theory]
    [InlineData("C", 0)]
    [InlineData("A", 3)]
    [InlineData("F#m", 3)]
    [InlineData("Bb", -2)]
    [InlineData("Ebm", -6)]
    [InlineData("C#", 7)]
    [InlineData("Cb", -7)]
    public void KeySignatures(string key, int signature)
    {
        Assert.Equal(signature, SheetKey.Parse(key).Signature);
    }
}
