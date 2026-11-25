using System.Text.Json;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using HarmonyDB.Source.Api.Model.V2.Contents;

namespace HarmonyDB.Shelf.Tests.SourceV2;

/// <summary>
/// The source contract's wire format: every content kind is read back as written, with the names the format fixes.
/// </summary>
public sealed class WireFormatTests
{
    private static readonly BlobRef Blob = new() { Key = "blob-1", ContentType = "audio/flac" };

    public static TheoryData<string, object> Kinds() => new()
    {
        { "sheet", new SheetContent { Root = Sheet(), AuthorComment = "capo 2" } },
        { "timeline", Timeline() },
        { "lyrics", Lyrics() },
        { "notes", Notes() },
        { "score", new Score { MusicXml = "<score-partwise/>", Times = new() { End = 4, Bars = [new() { Beats = [0, 1, 2, 3], TimeSignature = "4/4" }] } } },
        { "chart", Chart() },
        { "recording", Recording() },
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryKindRoundTrips(string kind, object content)
    {
        var json = JsonSerializer.Serialize(content, content.GetType(), SourceV2Json.Options);
        var back = JsonSerializer.Deserialize(json, content.GetType(), SourceV2Json.Options)!;
        Assert.Equal(json, JsonSerializer.Serialize(back, content.GetType(), SourceV2Json.Options));

        var answer = new GetContentResponse
        {
            Id = "x-1",
            State = ItemState.Open,
            Content = Wrap(kind, content),
        };
        var answerJson = JsonSerializer.Serialize(answer, SourceV2Json.Options);
        Assert.Equal(answerJson, JsonSerializer.Serialize(JsonSerializer.Deserialize<GetContentResponse>(answerJson, SourceV2Json.Options), SourceV2Json.Options));
        Assert.Contains($"\"{kind}\":{{", answerJson);
    }

    [Fact]
    public void NamesAreFixed()
    {
        var json = JsonSerializer.Serialize(Timeline(), SourceV2Json.Options) + JsonSerializer.Serialize(Recording(), SourceV2Json.Options)
                   + JsonSerializer.Serialize(new HeaderAnswer { Id = "x-1", State = ItemState.Unavailable }, SourceV2Json.Options)
                   + JsonSerializer.Serialize(new ResolvedUri { Uri = new("https://example.com/"), State = ResolveState.NotResolved }, SourceV2Json.Options);

        foreach (var name in new[] { "\"noChord\"", "\"youtube\"", "\"chorus\"", "\"guitar\"", "\"unavailable\"", "\"notResolved\"", "\"timeSignature\"" })
        {
            Assert.Contains(name, json);
        }
    }

    private static Content Wrap(string kind, object content) => kind switch
    {
        "sheet" => new() { Sheet = (SheetContent)content },
        "timeline" => new() { Timeline = (Timeline)content },
        "lyrics" => new() { Lyrics = (Lyrics)content },
        "notes" => new() { Notes = (Notes)content },
        "score" => new() { Score = (Score)content },
        "chart" => new() { Chart = (Chart)content },
        "recording" => new() { Recording = (Recording)content },
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static NodeHtml Sheet()
    {
        var root = new NodeHtml { Name = "pre" };
        root.Children.Add(new NodeChild(new NodeText("la la la")));
        return root;
    }

    private static Timeline Timeline() => new()
    {
        Duration = 13.5,
        Keys = [new() { Key = "Am", Start = 0, End = 6.125 }, new() { Key = "C", Start = 6.125, End = 13.5 }],
        Bars =
        [
            new() { Start = 0.5, End = 1.125, Beats = [0.5], TimeSignature = "4/4", Key = "Am", Tempo = new() { Bpm = 96, BeatUnit = new() { Denominator = 4 } } },
            new() { Start = 1.125, End = 3.625, Beats = [1.125, 1.75, 2.375, 3.0], TimeSignature = "2+2+3/8", Key = "Am", Fermatas = [3],
                Tempo = new() { Bpm = 192, BeatUnit = new() { Denominator = 8 } } },
        ],
        Chords =
        [
            new() { Start = 0.5, End = 1.125, Symbol = new() { Kind = ChordSymbolKind.Chord, Name = "E7" }, Degree = "V",
                Place = new() { Bar = 0, Beat = 1 }, Pitches = [40, 47, 50, 56], WrittenNotes = ["E2", "B2", "D3", "G#3"],
                Fingerings = [new() { Instrument = FingeringInstrument.Guitar, Frets = [0, 2, 0, 1, 0, 0], Fingers = [0, 2, 0, 1, 0, 0] }] },
            new() { Start = 1.125, End = 3.469, Symbol = new() { Kind = ChordSymbolKind.NoChord }, Place = new() { Bar = 1, Beat = 1 } },
            new() { Start = 3.469, End = 13.5, Symbol = new() { Kind = ChordSymbolKind.Stop }, Place = new() { Bar = 1, Beat = 4, At = new() { Numerator = 3, Denominator = 4 } } },
        ],
        Sections = [new() { Start = 0.5, End = 13, Label = SectionLabel.Chorus, Name = "Chorus 1b", Number = 1, Part = 2 }, new() { Start = 13, End = 13.5, Name = "Coda" }],
    };

    private static Lyrics Lyrics() => new()
    {
        Language = "en",
        Lines =
        [
            new() { Text = "Hello there", Start = 1, End = 2, Match = LyricsLineMatch.Heard,
                Words = [new() { Text = "Hello", Start = 1, End = 1.4 }, new() { Text = " there", Start = 1.4 }] },
            new() { Text = "Not sung", Match = LyricsLineMatch.NotFound, Words = [new() { Text = "Not" }, new() { Text = " sung" }] },
        ],
        Unwritten = [new() { Text = "oh oh", Start = 3, End = 4 }],
    };

    private static Notes Notes() => new()
    {
        Parts =
        [
            new() { Name = "Vocals", Stem = Stem.Vocals, Notes = [new() { Start = 0.8, End = 1.1, Pitch = 57, Velocity = 62 }] },
            new() { Name = "Acoustic guitar", Program = 24, Notes = [new() { Start = 1, End = 2, Pitch = 52 }] },
        ],
        Drums = [new() { Start = 0.5, Drum = 36 }],
        Midi = new() { Key = "midi-1", ContentType = "audio/midi" },
    };

    private static Chart Chart() => new()
    {
        BandChart = new()
        {
            TimeSignature = "4/4",
            Parts =
            [
                new() { Name = "Ch", Label = SectionLabel.Chorus, Bars =
                [
                    new() { Start = 0, End = 2, Chords = [new() { Kind = ChordSymbolKind.Chord, Name = "Am" }, new() { Kind = ChordSymbolKind.Stop }], Note = "pads" },
                ] },
            ],
        },
        IReal = "irealbook://Song=Artist=Style=C=n=[C   |G   ]",
    };

    private static Recording Recording() => new()
    {
        Audio = new() { Track = new() { Service = MusicService.Youtube, Id = "track-1" } },
        Duration = 212.5,
        Stems = [new() { Stem = Stem.Drums, Audio = new() { Blob = Blob } }],
    };
}
