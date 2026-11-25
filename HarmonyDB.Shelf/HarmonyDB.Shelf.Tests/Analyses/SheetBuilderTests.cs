using System.Text;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Shelf.Api.Areas.Analyses.Services.Sheets;
using HarmonyDB.Source.Api.Model.V2.Contents;

namespace HarmonyDB.Shelf.Tests.Analyses;

/// <summary>
/// The chord sheet of an analysis, on small made-up songs: bars of 4/4, a beat a second (a bar is 4 seconds). Without
/// a key, unless a test gives one, so that a chord takes the room of its name alone.
/// </summary>
public sealed class SheetBuilderTests
{
    [Fact]
    public void ChordsStandOverTheCharacterSungWhenTheyBegin()
    {
        // "Hello world": Hello 8–10 s, world 10–12 s. C begins before the line and still sounds when it begins; G a
        // quarter into "world"; F between the lines, sounding when the next one begins
        var sheet = Build(
            Chords((0, "C"), (10.5, "G"), (12.5, "F")),
            Lines(Line((8, 10, "Hello"), (10, 12, "world")), Line((13, 14, "again"))));

        Assert.Equal(
            """
            [4/4 60]
            C      G
            Hello world
            F
            again
            """,
            Text(sheet));
    }

    [Fact]
    public void AShortTurnBetweenLinesEndsTheLineBeforeIt()
    {
        // G is played alone between the lines (bar 4); C begins before the next line and still sounds when it begins
        var sheet = Build(
            Chords((8, "C"), (12, "G"), (16, "C")),
            Lines(Line((8, 9, "one"), (9, 10, "two")), Line((20, 21, "three"))));

        Assert.Equal(
            """
            [4/4 60]
            C        G
            one two
            C
            three
            """,
            Text(sheet));
    }

    [Fact]
    public void WordsArePushedApartForTheChords()
    {
        // two chords on two short words: the second word moves right whole, with the rest of the line
        var sheet = Build(
            Chords((8, "Cmaj7"), (8.6, "Am7")),
            Lines(Line((8, 8.5, "I"), (8.5, 9, "am"), (9, 10, "here"))));

        Assert.Equal(
            """
            [4/4 60]
            Cmaj7 Am7
            I     am here
            """,
            Text(sheet));
    }

    [Theory]
    // the sections on the bar lines
    [InlineData(0, 16, 32)]
    // the sections a little after them, as a section's boundaries are heard: put on the bar lines, nothing moves
    [InlineData(0.4, 17.3, 32.9)]
    public void ChordsAloneAreLinesOfFourBarsCutWhereASectionBegins(double intro, double verse, double end)
    {
        // an intro of four bars and two more before the line, a chord a bar; the verse begins at bar 5 (16 s), its
        // first line at bar 7 (24 s)
        var sheet = Build(
            Chords((0, "Am"), (4, "F"), (8, "C"), (12, "G"), (16, "Am"), (20, "E"), (24, "Am")),
            Lines(Line((24, 26, "Sing"))),
            sections: [Section(SectionLabel.Intro, null, intro, verse), Section(SectionLabel.Verse, 1, verse, end)]);

        Assert.Equal(
            """
            [4/4 60]
            Intro
            Am  F  C  G

            Verse
            Am  E
            Am
            Sing
            """,
            Text(sheet));
    }

    [Fact]
    public void ASectionsNameIsItsOwnElseMadeOfItsLabelAndPlace()
    {
        // a bar each: neighbours of one label are parts of one section, a label of more than one section is numbered, a
        // name given is shown as it is (in any language, whatever the words' language is)
        var sheet = Build(
            Chords((0, "C"), (4, "D"), (8, "E"), (12, "F"), (16, "G"), (20, "A"), (24, "B")),
            Lines(Line((30, 31, "la"))) with { Language = "ru" },
            sections:
            [
                Section(SectionLabel.Intro, null, 0, 4), Section(SectionLabel.Verse, null, 4, 8), Section(SectionLabel.Verse, null, 8, 12),
                Section(SectionLabel.Chorus, null, 12, 16), new() { Label = SectionLabel.Verse, Name = "Куплет 2", Start = 16, End = 20 },
                Section(SectionLabel.Outro, null, 20, 24), Section(SectionLabel.Outro, null, 24, 28),
            ]);

        Assert.Equal(
            """
            [4/4 60]
            Intro
            C

            Verse 1
            D

            Verse 1b
            E

            Chorus
            F

            Куплет 2
            G

            Outro
            A

            Outro b
            B
            la
            """,
            Text(sheet));
    }

    [Fact]
    public void TwoBoundariesOnOneBarLineAreOneAndWithoutBarsThereAreNoSections()
    {
        // the intro ends, and the verse begins, before the first bar line is half a bar away: the verse begins there
        var sections = new[] { Section(SectionLabel.Intro, null, 0, 0.9), Section(SectionLabel.Verse, null, 0.9, 16) };
        var sheet = Build(Chords((0, "C"), (4, "G")), null, sections: sections);
        Assert.Equal(
            """
            [4/4 60]
            Verse
            C  G
            """,
            Text(sheet));

        var noBars = Build(Chords((0, "C"), (4, "G")), null, sections: sections, bars: []);
        Assert.DoesNotContain(noBars.Children, x => x.NodeHtml != null);
    }

    [Fact]
    public void TheTimelinesSectionsCarryTheNamesOfTheSheetsHeaders()
    {
        // the intro is gone from the sheet (the verse begins on its bar line); the rest named as the headers are, a name
        // given as it is; the times and the rest of each section as they were
        var sections = new[]
        {
            Section(SectionLabel.Intro, null, 0, 0.9), Section(SectionLabel.Verse, null, 0.9, 4.1), Section(SectionLabel.Verse, null, 4.1, 8),
            new() { Label = SectionLabel.Chorus, Name = "Припев", Start = 8, End = 12 }, Section(SectionLabel.Verse, null, 12, 16),
        };
        var song = Song(Chords((0, "C"), (4, "D"), (8, "E"), (12, "F")), sections);

        var named = SheetBuilder.WithSectionNames(song);
        Assert.Equal([null, "Verse 1", "Verse 1b", "Припев", "Verse 2"], named.Sections.Select(x => x.Name));
        Assert.Equal(sections.Select(x => x with { Name = null }), named.Sections.Select(x => x with { Name = null }));
        Assert.Equal(["Verse 1", "Verse 1b", "Припев", "Verse 2"], Headers(SheetBuilder.Build(song, null)));

        // without bars the sheet has no sections: the names stay as they are
        Assert.Equal([null, null, null, "Припев", null], SheetBuilder.WithSectionNames(song with { Bars = [] }).Sections.Select(x => x.Name));
    }

    private static IEnumerable<string> Headers(NodeHtml sheet)
        => sheet.Children.Where(x => x.NodeHtml is { Name: "b" }).Select(x => string.Concat(x.NodeHtml!.Children.Select(c => c.NodeText!.Text)));

    [Fact]
    public void ALineSungFromBeforeTheBarLineIsInWhatItLeadsInto()
    {
        // "I believe" is sung before the chorus begins (16 s), "fly" on its first beat; the next line comes at 24 s
        var sheet = Build(
            Chords((0, "C"), (16, "G")),
            Lines(Line((4, 6, "verse")), Line((13, 14, "I"), (14, 16, "believe"), (16, 18, "fly")), Line((24, 25, "again"))),
            sections: [Section(SectionLabel.Verse, null, 0, 16), Section(SectionLabel.Chorus, null, 16, 64)]);

        Assert.Equal(
            """
            [4/4 60]
            Verse
            C
            verse

            Chorus
                      G
            I believe fly
            again
            """,
            Text(sheet));
    }

    [Fact]
    public void WordsWithoutTimesShareTheTimeOfTheirLine()
    {
        // "the" and "winner" have no times: they share 9–12 s, and G at 11 s stands in "winner"
        var line = new LyricsLine
        {
            Text = "so the winner",
            Start = 8,
            End = 12,
            Words = [new() { Text = "so", Start = 8, End = 9 }, new() { Text = " the" }, new() { Text = " winner" }],
        };
        var sheet = Build(Chords((8, "C"), (11, "G")), Lines(line));

        Assert.Equal(
            """
            [4/4 60]
            C        G
            so the winner
            """,
            Text(sheet));
    }

    [Fact]
    public void AChangeWhileALineIsSungDoesNotCutIt()
    {
        // a bar of 2/4 (4–6 s) while a line is sung: the line stays whole; a bar of 2/4 between lines (10–12 s) is marked
        // where it is
        Bar Bar(double start, double end, string meter) => new()
        {
            Start = start, End = end, Beats = Enumerable.Range(0, (int)(end - start)).Select(i => start + i).ToList(), TimeSignature = meter,
            Tempo = new() { Bpm = 60, BeatUnit = new() { Denominator = 4 } },
        };
        var sheet = Build(
            Chords((0, "C")),
            Lines(Line((3, 4, "one"), (4, 5, "two"), (6, 7, "three")), Line((14, 15, "four"))),
            bars: [Bar(0, 4, "4/4"), Bar(4, 6, "2/4"), Bar(6, 10, "4/4"), Bar(10, 12, "2/4"), Bar(12, 16, "4/4")]);

        Assert.Equal(
            """
            [4/4 60]
            C
            one two three
            [2/4]
            [4/4]
            four
            """,
            Text(sheet));
    }

    [Theory]
    // most of the line before the chorus and its key: it stays in the verse, whole; the marks come after it
    [InlineData(12, 14, 15, 17, 18, """
                                    [C 4/4 60]
                                    Verse
                                    C   G
                                    a b c

                                    Chorus
                                    [G]
                                    d
                                    """)]
    // most of it after: it is the chorus's, whole, under its header and the key's mark; the chord before it is the verse's
    [InlineData(14, 15, 16, 20, 30, """
                                    [C 4/4 60]
                                    Verse
                                    C

                                    Chorus
                                    [G]
                                        G
                                    a b c
                                    d
                                    """)]
    public void ALineAcrossASectionAndAKeyStaysWhole(double a, double b, double c, double end, double next, string expected)
    {
        var sheet = Build(
            Chords((12, "C"), (16, "G")),
            Lines(Line((a, b, "a"), (b, c, "b"), (c, end, "c")), Line((next, next + 1, "d"))),
            sections: [Section(SectionLabel.Verse, null, 0, 16), Section(SectionLabel.Chorus, null, 16, 64)],
            keys: [new() { Key = "C", Start = 0, End = 16 }, new() { Key = "G", Start = 16, End = 64 }]);

        Assert.Equal(expected.ReplaceLineEndings("\n"), Text(sheet));
    }

    [Fact]
    public void ADegreeIsTheMaterialsWhenItGivesOne()
    {
        var timeline = new Timeline
        {
            Duration = 8,
            Keys = [new() { Key = "D", Start = 0, End = 8 }],
            Bars = [],
            Chords =
            [
                new() { Start = 0, End = 4, Symbol = new() { Kind = ChordSymbolKind.Chord, Name = "Gm6/A" }, Degree = "IV / bVI" },
                new() { Start = 4, End = 8, Symbol = new() { Kind = ChordSymbolKind.Chord, Name = "A7" }, Degree = "V / V" },
            ],
            Sections = [],
        };

        var chords = SheetBuilder.Build(timeline, null).TraverseAllChords().Select(x => (x.Degree, x.BassDegree)).ToList();

        // the first as given; the second's is not a degree of a chord without a bass: worked out
        Assert.Equal([("IV", "bVI"), ("V", (string?)null)], chords);
    }

    [Fact]
    public void KeyChangesAreMarkedAndDegreesAreInTheirKey()
    {
        var sheet = Build(
            Chords((0, "Am"), (4, "E7/G#"), (8, "C"), (12, "G7")),
            null,
            keys: [new() { Key = "Am", Start = 0, End = 8 }, new() { Key = "C", Start = 8, End = 64 }]);

        var marks = sheet.Children.Select(x => x.NodeMark).Where(x => x != null).ToList();
        Assert.Equal(new SheetTimeSignature(4, 4), marks[0]!.TimeSignature);
        Assert.Equal(new SheetTempo(60), marks[0]!.Tempo);
        Assert.Null(marks[1]!.Tempo);
        Assert.Equal(8, marks[1]!.Time);
        Assert.Equal(["Am", "C"], sheet.Keys!.Select(x => x.Name));

        var chords = sheet.TraverseAllChords().Select(x => (Name(x), x.Degree, x.BassDegree)).ToList();
        Assert.Equal([("Am", "I", null), ("E7/G#", "V", "#VII"), ("C", "I", null), ("G7", "V", null)], chords);

        // a chord takes the room of its degree too: "Am" and "I", "E7/G#" and "V/#VII"
        Assert.Equal(
            """
            [Am 4/4 60]
            Am   E7/G#
            [C]
            C   G7
            """,
            Text(sheet));
    }

    [Fact]
    public void ARestIsNoChordOnce()
    {
        // before the first chord nothing sounds; no chord and a stop after it are one rest
        var sheet = Build([NoChord(0), Chord(4, "D"), NoChord(8), Stop(9), Chord(12, "D")], null);

        Assert.Equal(
            """
            [4/4 60]
            D  N.C.  D
            """,
            Text(sheet));
    }

    [Fact]
    public void ARenderedSheetHasItsChordsOverItsWordsAndItsChordsAloneApart()
    {
        var sheet = Build(
            Chords((0, "Am"), (4, "F"), (8, "C")),
            Lines(Line((8, 10, "la"), (10, 12, "la"))),
            sections: [Section(SectionLabel.Intro, null, 0, 8), Section(SectionLabel.Chorus, null, 8, 16)]);

        var html = sheet.AsHtml(new());
        Assert.Contains("<div class='chords-double-line'>", html);
        Assert.Contains("data-time='8'", html);
        Assert.Contains("<span data-time='10'>la</span>", html);

        // the chords of the intro are a line of their own, not over the header's word
        var chorus = html.IndexOf("<b>Chorus</b>", StringComparison.Ordinal);
        Assert.True(html.IndexOf("<b>Intro</b>", StringComparison.Ordinal) < html.IndexOf(">Am<", StringComparison.Ordinal));
        Assert.True(html.IndexOf(">Am<", StringComparison.Ordinal) < chorus);
        Assert.DoesNotContain("flying", html[..chorus]);
    }

    [Fact]
    public void AnUnreadableChordIsRefused()
    {
        var exception = Assert.Throws<UnreadableChordException>(() => Build(Chords((0, "Qm")), null));
        Assert.Equal("Qm", exception.Name);
    }

    [Theory]
    [InlineData("C#", "F", "#V")]
    [InlineData("Db", "Gm", "bV")]
    [InlineData("G#", "Am", "#VII")]
    [InlineData("A", "Bm", "VII")]
    [InlineData("Bb", "C", "bVII")]
    [InlineData("F##", "G#m", "#VII")]
    [InlineData("Cb", "Gb", "IV")]
    public void Degrees(string note, string key, string degree)
    {
        var (root, _) = ChordReading.Read(note);
        Assert.Equal(degree, ChordReading.Degree(root, SheetKey.Parse(key)));
    }

    // ------------------------------------------------------------------ made-up songs

    private static NodeHtml Build(IReadOnlyList<TimelineChord> chords, Lyrics? lyrics, IReadOnlyList<Section>? sections = null, IReadOnlyList<KeySpan>? keys = null, IReadOnlyList<Bar>? bars = null)
        => SheetBuilder.Build(Song(chords, sections, keys, bars), lyrics);

    private static Timeline Song(IReadOnlyList<TimelineChord> chords, IReadOnlyList<Section>? sections = null, IReadOnlyList<KeySpan>? keys = null, IReadOnlyList<Bar>? bars = null)
    {
        const int barCount = 16;
        return new Timeline
        {
            Duration = barCount * 4,
            Keys = keys ?? [],
            Bars = bars ?? Enumerable.Range(0, barCount).Select(i => new Bar
            {
                Start = i * 4,
                End = i * 4 + 4,
                Beats = [i * 4, i * 4 + 1, i * 4 + 2, i * 4 + 3],
                TimeSignature = "4/4",
                Tempo = new() { Bpm = 60, BeatUnit = new() { Denominator = 4 } },
            }).ToList(),
            Chords = chords,
            Sections = sections ?? [],
        };
    }

    private static IReadOnlyList<TimelineChord> Chords(params (double time, string name)[] chords)
        => chords.Select(x => Chord(x.time, x.name)).ToList();

    private static TimelineChord Chord(double time, string name)
        => new() { Start = time, End = time + 1, Symbol = new() { Kind = ChordSymbolKind.Chord, Name = name } };

    private static TimelineChord NoChord(double time) => new() { Start = time, End = time + 1, Symbol = new() { Kind = ChordSymbolKind.NoChord } };

    private static TimelineChord Stop(double time) => new() { Start = time, End = time + 1, Symbol = new() { Kind = ChordSymbolKind.Stop } };

    private static Lyrics Lines(params LyricsLine[] lines) => new() { Lines = lines };

    private static LyricsLine Line(params (double start, double end, string text)[] words) => new()
    {
        Text = string.Join(' ', words.Select(x => x.text)),
        Start = words[0].start,
        End = words[^1].end,
        Words = words.Select((x, i) => new LyricsWord { Text = (i > 0 ? " " : "") + x.text, Start = x.start, End = x.end }).ToList(),
    };

    private static Section Section(SectionLabel label, int? number, double start, double end)
        => new() { Label = label, Number = number, Start = start, End = end };

    private static string Name(NodeChord chord) => string.Concat(chord.Children.Select(x => x.NodeText!.Text));

    /// <summary>
    /// The sheet as plain text: a chord by its name, a header by its words, a mark as what it says in brackets.
    /// </summary>
    private static string Text(NodeHtml sheet)
    {
        var builder = new StringBuilder();
        foreach (var child in sheet.Children)
        {
            builder.Append(child.AsNode() switch
            {
                NodeText text => text.Text,
                NodeChord chord => Name(chord),
                NodeHtml html => string.Concat(html.Children.Select(x => x.NodeText!.Text)),
                NodeMark mark => $"[{string.Join(' ', new object?[] { mark.Key?.Name, mark.TimeSignature, mark.Tempo?.BeatsPerMinute }.Where(x => x != null))}]",
                _ => throw new ArgumentOutOfRangeException(nameof(sheet)),
            });
        }

        return builder.ToString();
    }
}
