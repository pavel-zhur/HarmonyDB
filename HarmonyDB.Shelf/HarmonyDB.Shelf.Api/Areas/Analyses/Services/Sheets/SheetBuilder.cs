using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Source.Api.Model.V2.Contents;

namespace HarmonyDB.Shelf.Api.Areas.Analyses.Services.Sheets;

/// <summary>
/// Makes the chord sheet of an analysis — text with chords, what a person plays from — of its timeline and its words.
///
/// - The lines of the words as they are sung, one line of the sheet each, the words one space apart. Nothing is spaced
///   by time.
/// - A chord stands over the character sung when it begins: as far into the word as the chord is into the word's time;
///   in a pause inside a line, over the next word. The sheet is monospace, so a place is a column; a chord takes the
///   room of its name and its degree. When a chord would stand inside the room of the chord before it, on another word,
///   the words are pushed apart (spaces before its word, which moves right whole with its chord, and the rest of the
///   line with it); in the same word, the chord stands right after the one before it. A word is never torn, no chord is
///   dropped.
/// - A chord that begins between two lines and still sounds when the next line begins stands over that line's start.
///   A short turn of chords between two lines (under two bars, in one section, nothing changing) ends the line before
///   it, after its words. The other chords where nothing is sung (an intro, a solo, between lines, the end) are lines of
///   chords alone, a few columns apart, four bars a line (fewer when they are busy), a new line where a section begins
///   and where the key, the tempo or the meter changes. A section that begins while a line's last word is held begins
///   there: what is played from then on is not the line's.
/// - A chord written again (struck again) stands again; no chord and a stop are "N.C.", once for a run of them.
/// - The sections are put on the bar lines first, and named (<see cref="OnBarLines"/>).
/// - A line lasts until the next line of the sheet begins, and is in the section, the key, the meter and the tempo
///   where its middle is: a line sung from before a bar line (a pickup) is in what it leads into. Every section has its
///   header, its name as it is, before its first line; a
///   section where no line begins has its header alone, in its place. The marks of a key, a tempo or a meter stand on
///   lines of their own, under the header. A sung line is never cut: a change while it is sung is marked before it or
///   after it, by where most of the line is; a change between lines (a bar of another meter where nothing is sung) has
///   its own mark there. The first mark is at the top: the key, the meter and the tempo in force where the first chord
///   sounds.
/// - A chord has its degree, and its bass's — as the material gives them, else worked out in the key where it stands —
///   and its time; a word has its time when it has its own. Words without times share the time between the words
///   around them.
/// - A line of the words that has no time at all stands after the words sung before it, without chords.
/// </summary>
public static class SheetBuilder
{
    public const string NoChord = "N.C.";

    /// <summary>
    /// Columns between two chords on a line of words, and between two chords of a line of chords alone.
    /// </summary>
    private const int ChordGap = 1, RunGap = 2;

    /// <summary>
    /// Bars to a line of chords alone; chords without bars, so many to a line.
    /// </summary>
    private const int BarsPerLine = 4, ChordsPerLine = 8;

    /// <summary>
    /// The widest line of chords alone, columns: four busy bars make two lines.
    /// </summary>
    private const int MaxColumns = 56;

    /// <summary>
    /// A turn of chords between two lines that ends the line before it: of fewer bars than this (so many chords,
    /// without bars).
    /// </summary>
    private const int TurnBars = 2;

    /// <summary>
    /// Seconds: a chord this close before a section's start or a change begins it.
    /// </summary>
    private const double Near = 0.02;

    /// <exception cref="UnreadableChordException">A chord's name is not readable.</exception>
    public static NodeHtml Build(Timeline timeline, Lyrics? lyrics)
    {
        var song = new Song(OnBarLines(timeline));
        var chords = song.ChordEvents();
        var lines = LyricLines(lyrics);
        var rows = Rows(song, chords, lines);
        return Write(song, rows);
    }

    /// <summary>
    /// The sections as the sheet has them: on the bar lines (a bar's start, or where the last bar ends), each named.
    /// - Each boundary — a section's start, and its end where the next section does not begin at once — goes to the bar
    ///   line nearest to it; of two as near, the earlier.
    /// - Two boundaries on one line are one: what begins at the later of them begins there. A section runs from its line
    ///   to the next boundary's line (or the last line); one left with no bar of its own is gone.
    /// - Its name is its own, as given. Without one it is named of its label and its place: neighbours of one label are
    ///   the parts of one section, lettered from the second ("Verse 1b"; "Outro b" without a number); a label that
    ///   occurs more than once is numbered in order ("Verse 1", "Verse 2"); the label's word with a capital.
    /// - Without bars there are no sections.
    /// </summary>
    private static Timeline OnBarLines(Timeline timeline)
    {
        if (timeline.Bars.Count == 0)
            return timeline with { Sections = [] };

        var lines = timeline.Bars.Select(x => x.Start).Append(timeline.Bars[^1].End).ToList();
        int Nearest(double time) => Enumerable.Range(0, lines.Count).MinBy(i => Math.Abs(lines[i] - time));

        // the boundaries on the lines: a section that begins there, or null where one ends with none after it at once
        var placed = new SortedDictionary<int, Section?>();
        double? end = null;
        foreach (var section in timeline.Sections)
        {
            if (section.Label == null && string.IsNullOrWhiteSpace(section.Name))
                throw new ArgumentOutOfRangeException(nameof(timeline), "A section has a label or a name.");

            if (end.HasValue && end != section.Start) placed[Nearest(end.Value)] = null;
            placed[Nearest(section.Start)] = section;
            end = section.End;
        }

        if (end.HasValue) placed[Nearest(end.Value)] = null;

        var marks = placed.ToList();
        var sections = new List<(int from, int to, Section section)>();
        for (var i = 0; i < marks.Count; i++)
        {
            var to = i + 1 < marks.Count ? marks[i + 1].Key : lines.Count - 1;
            if (marks[i].Value is { } section && to > marks[i].Key)
                sections.Add((marks[i].Key, to, section));
        }

        // neighbours of one label are the parts of one section; a label of more than one section is numbered
        var runs = new List<List<(int from, int to, Section section)>>();
        foreach (var x in sections)
        {
            if (runs.Count > 0 && x.section.Label != null && runs[^1][^1].section.Label == x.section.Label && runs[^1][^1].to == x.from)
                runs[^1].Add(x);
            else
                runs.Add([x]);
        }

        var total = runs.Where(x => x[0].section.Label != null).GroupBy(x => x[0].section.Label).ToDictionary(x => x.Key!.Value, x => x.Count());
        var seen = new Dictionary<SectionLabel, int>();
        var named = new List<Section>();
        foreach (var run in runs)
        {
            var label = run[0].section.Label;
            int? number = null;
            if (label.HasValue)
            {
                seen[label.Value] = seen.GetValueOrDefault(label.Value) + 1;
                number = total[label.Value] > 1 ? seen[label.Value] : null;
            }

            for (var i = 0; i < run.Count; i++)
            {
                var (from, to, section) = run[i];
                var part = label.HasValue && run.Count > 1 ? i + 1 : (int?)null;
                var letter = i > 0 ? ((char)('a' + Math.Min(i, 25))).ToString() : string.Empty;
                var made = label.HasValue
                    ? label.Value + (number.HasValue ? $" {number}{letter}" : letter.Length > 0 ? $" {letter}" : string.Empty)
                    : null;
                named.Add(section with
                {
                    Start = lines[from],
                    End = lines[to],
                    Number = number,
                    Part = part,
                    Name = string.IsNullOrWhiteSpace(section.Name) ? made : section.Name,
                });
            }
        }

        return timeline with { Sections = named };
    }

    // ------------------------------------------------------------------ the chords

    private sealed record ChordEvent(double Time, string Name, string? Degree, string? BassDegree)
    {
        /// <summary>
        /// The columns it takes: its name, and its degree as the sheet shows it after the name.
        /// </summary>
        public int Width => Name.Length + (Degree == null ? 0 : Degree.Length + (BassDegree == null ? 0 : 1 + BassDegree.Length));
    }

    // ------------------------------------------------------------------ the words

    private sealed record Word(string Text, double Start, double End, double? OwnTime);

    /// <summary>
    /// A line of the words: with its words placed in time (<see cref="Words"/>), or without a time (<see cref="Text"/>).
    /// </summary>
    private sealed record LyricLine(IReadOnlyList<Word>? Words, string? Text)
    {
        public double Start => Words![0].Start;

        public double End => Words![^1].End;
    }

    /// <summary>
    /// The lines as sung. Words without a time of their own share evenly the time between the words around them (at the
    /// line's edges, the line's own start or end); a line whose words have no times shares its time among them; a line
    /// with no time at all has none. A word's time is kept on the sheet only when it is its own.
    /// </summary>
    private static IReadOnlyList<LyricLine> LyricLines(Lyrics? lyrics)
    {
        var result = new List<LyricLine>();
        foreach (var line in lyrics?.Lines ?? [])
        {
            var places = new List<(string text, double? start, double? end)>();
            if (line.Words.Count > 0)
            {
                places.AddRange(line.Words.Select(w => (w.Text.Trim(), w.Start, w.End)).Where(w => w.Item1.Length > 0));
            }
            else
            {
                var parts = line.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                var each = line is { Start: { } s, End: { } e } ? (e - s) / Math.Max(1, parts.Length) : (double?)null;
                places.AddRange(parts.Select((text, i) => (text, line.Start + i * each, line.Start + (i + 1) * each)));
            }

            if (places.Count == 0)
                continue;

            if (places.All(x => !x.start.HasValue) && !(line.Start.HasValue && line.End.HasValue))
            {
                result.Add(new(null, string.Join(' ', places.Select(x => x.text))));
                continue;
            }

            // the words without a start share the time between the words around them (the line's own start and end at
            // its edges) evenly; a word without an end lasts until the next one begins
            var starts = places.Select(x => x.start).ToArray();
            for (var i = 0; i < starts.Length; i++)
            {
                if (starts[i].HasValue) continue;
                var j = i;
                while (j < starts.Length && !starts[j].HasValue) j++;
                var from = i > 0 ? places[i - 1].end ?? starts[i - 1]!.Value : line.Start ?? starts[j]!.Value;
                var to = j < starts.Length ? starts[j]!.Value : line.End ?? from;
                if (to < from) to = from;
                for (var k = i; k < j; k++)
                    starts[k] = from + (to - from) * (k - i) / (j - i);
                i = j - 1;
            }

            var words = new List<Word>();
            for (var i = 0; i < places.Count; i++)
            {
                var (text, start, end) = places[i];
                var t = starts[i]!.Value;
                var next = i + 1 < places.Count ? starts[i + 1]!.Value : line.End ?? t;
                var e = start.HasValue && end > start ? end!.Value : Math.Max(t, next);
                words.Add(new(text, t, e, start));
            }

            result.Add(new(words, null));
        }

        return result;
    }

    // ------------------------------------------------------------------ the rows

    private abstract record Row;

    /// <summary>
    /// A line of words with the chords over them, at their columns.
    /// </summary>
    private sealed record WordsRow(LyricLine Line, IReadOnlyList<(Word word, int column)> Words, IReadOnlyList<(ChordEvent chord, int column)> Chords) : Row;

    /// <summary>
    /// A line of chords where nothing is sung.
    /// </summary>
    private sealed record ChordsRow(IReadOnlyList<(ChordEvent chord, int column)> Chords) : Row;

    /// <summary>
    /// A line of words that has no time.
    /// </summary>
    private sealed record TextRow(string Text) : Row;

    private static IReadOnlyList<Row> Rows(Song song, IReadOnlyList<ChordEvent> chords, IReadOnlyList<LyricLine> all)
    {
        var timed = all.Where(x => x.Words != null).ToList();

        // A line is sung until its last word ends, but a part where nothing is sung (an instrumental, a solo …) that begins
        // on its last word, or while it is held, begins there: what is played from then on is that part's, not the line's.
        var ends = timed.Select(line => song.Sections
            .Where(x => x.Label is SectionLabel.Intro or SectionLabel.Instrumental or SectionLabel.Solo or SectionLabel.Break or SectionLabel.Outro)
            .Select(x => x.Start)
            .Where(s => line.Start + Near < s && line.Words![^1].Start - Near <= s && s < line.End)
            .DefaultIfEmpty(line.End)
            .Min()).ToList();

        // every chord: on a line (its word and the place in it), or alone before a line, or after the last
        var on = timed.Select(_ => new List<(ChordEvent chord, int word, int offset)>()).ToList();
        var alone = timed.Select(_ => new List<ChordEvent>()).ToList();
        var tail = new List<ChordEvent>();
        var li = -1;
        for (var k = 0; k < chords.Count; k++)
        {
            var c = chords[k];
            while (li + 1 < timed.Count && timed[li + 1].Start <= c.Time) li++;
            var line = li >= 0 ? timed[li] : null;
            var next = li + 1 < timed.Count ? timed[li + 1] : null;
            var after = k + 1 < chords.Count ? chords[k + 1] : null;

            if (line != null && c.Time < ends[li])
            {
                var (word, offset) = PlaceInLine(line, c.Time);
                on[li].Add((c, word, offset));
            }
            else if (next != null && !(after != null && after.Time <= next.Start)
                     && !song.SectionStarts.Any(s => c.Time + Near < s && s < next.End))
            {
                // the chord the next line begins on — unless a section begins between the chord and that line's end:
                // then it is the earlier section's, a line of chords alone there
                on[li + 1].Add((c, 0, 0));
            }
            else
            {
                (next != null ? alone[li + 1] : tail).Add(c);
            }
        }

        // a short turn of chords between two lines, with no section or change between, ends the line before it
        var trailing = timed.Select(_ => new List<ChordEvent>()).ToList();
        for (var i = 1; i < timed.Count; i++)
        {
            var run = alone[i];
            if (run.Count == 0) continue;
            var from = ends[i - 1];
            var spans = song.BarAt(run[0].Time + Near) is { } first && song.BarAt(run[^1].Time + Near) is { } last
                ? last - first < TurnBars
                : run.Count <= TurnBars;
            if (spans && !song.Breaks.Any(b => from - Near < b && b <= run[^1].Time + Near))
            {
                trailing[i - 1].AddRange(run);
                run.Clear();
            }
        }

        var rows = new List<Row>();
        var t = 0;
        foreach (var line in all)
        {
            if (line.Words == null)
            {
                rows.Add(new TextRow(line.Text!));
                continue;
            }

            rows.AddRange(ChordRows(song, alone[t]));
            rows.Add(WordsRowOf(line, on[t], trailing[t]));
            t++;
        }

        rows.AddRange(ChordRows(song, tail));
        return rows;
    }

    /// <summary>
    /// The place of a chord in a line sung at its moment: the word, and how far into it.
    /// </summary>
    private static (int word, int offset) PlaceInLine(LyricLine line, double time)
    {
        var words = line.Words!;
        var i = 0;
        while (i + 1 < words.Count && words[i + 1].Start <= time) i++;
        var w = words[i];
        if (time < w.Start) return (i, 0);
        if (time < w.End) return (i, Math.Min(w.Text.Length - 1, (int)Math.Floor((time - w.Start) / (w.End - w.Start) * w.Text.Length)));
        return (Math.Min(i + 1, words.Count - 1), 0);
    }

    /// <summary>
    /// A line's words with its chords: when a chord's place is inside the room of the chord before it, on another word
    /// the word moves right whole (and all after it); in the same word the chord alone moves, off its character.
    /// </summary>
    private static WordsRow WordsRowOf(LyricLine line, IReadOnlyList<(ChordEvent chord, int word, int offset)> placed, IReadOnlyList<ChordEvent> trailing)
    {
        var words = new List<(Word, int)>();
        var chords = new List<(ChordEvent, int)>();
        int column = 0, next = int.MinValue, lastWord = -1, p = 0;
        for (var j = 0; j < line.Words!.Count; j++)
        {
            var w = line.Words[j];
            var at = column;
            while (p < placed.Count && placed[p].word == j)
            {
                var (c, _, offset) = placed[p];
                var col = at + offset;
                if (col < next)
                {
                    if (j != lastWord) at += next - col;
                    col = next;
                }

                chords.Add((c, col));
                next = col + c.Width + ChordGap;
                lastWord = j;
                p++;
            }

            words.Add((w, at));
            column = at + w.Text.Length + 1;
        }

        // after the words, as chords alone stand
        var after = Math.Max(column - 1 + RunGap, next);
        foreach (var c in trailing)
        {
            chords.Add((c, after));
            after += c.Width + RunGap;
        }

        return new(line, words, chords);
    }

    /// <summary>
    /// Chords alone: one after another, four bars a line (so many chords, without bars), a new line where a section
    /// begins and where the key, the tempo or the meter changes.
    /// </summary>
    private static IEnumerable<ChordsRow> ChordRows(Song song, IReadOnlyList<ChordEvent> chords)
    {
        var row = new List<(ChordEvent, int)>();
        int column = 0, firstBar = 0;
        for (var k = 0; k < chords.Count; k++)
        {
            var c = chords[k];
            var bar = song.BarAt(c.Time + Near);
            var full = (bar.HasValue ? bar.Value >= firstBar + BarsPerLine : row.Count >= ChordsPerLine) || column + c.Width > MaxColumns;
            if (row.Count > 0 && (full || song.Breaks.Any(b => chords[k - 1].Time < b - Near && c.Time >= b - Near)))
            {
                yield return new(row);
                row = [];
                column = 0;
            }

            if (row.Count == 0) firstBar = bar ?? 0;
            row.Add((c, column));
            column += c.Width + RunGap;
        }

        if (row.Count > 0)
            yield return new(row);
    }

    // ------------------------------------------------------------------ the sheet

    private static NodeHtml Write(Song song, IReadOnlyList<Row> rows)
    {
        var keys = song.Keys.Where(x => x.Key != null).Select(x => SheetKey.Parse(x.Key!)).Distinct().ToList();
        var sheet = new NodeHtml { Name = "pre", Keys = keys.Count > 0 ? keys : null };
        var lines = new List<List<NodeBase>>();

        // the key, the meter and the tempo in force where the first chord sounds
        var state = song.StateAt(song.FirstChord is { } first ? first + Near : 0);
        var start = state.Mark(null);
        if (start != null) lines.Add([start]);

        // A line of words lasts until the next row begins; it is in the section, the key, the meter and the tempo where its
        // middle is: a line sung from before a bar line (a pickup) is so in what it leads into. A line of chords alone is
        // cut wherever these change, so it is in those where its first chord sounds.
        var starts = rows.Select(row => row switch
        {
            WordsRow x => x.Line.Start,
            ChordsRow x => x.Chords[0].chord.Time,
            _ => (double?)null,
        }).ToList();
        var middles = starts.Select((start, i) => start.HasValue
            ? rows[i] is ChordsRow
                ? start.Value + Near
                : (start.Value + Math.Max(start.Value, starts.Skip(i + 1).FirstOrDefault(x => x.HasValue) ?? song.Duration)) / 2
            : (double?)null).ToList();

        // every section has its header: before the first row whose middle is not before its start (a section where no
        // row begins has its header alone, where it begins among the rows)
        var sections = song.Sections;
        var header = 0;
        void Headers(double until)
        {
            for (; header < sections.Count && sections[header].Start - Near <= until; header++)
            {
                // An empty line before a header (not right under the marks at the top): it also keeps a line of
                // chords alone off the header's words.
                if (!lines.All(x => x is [NodeMark])) lines.Add([]);
                lines.Add([new NodeHtml { Name = "b", Children = { new NodeText(sections[header].Name!).AsChild() } }]);
            }
        }

        Row? previous = null;
        var since = song.FirstChord ?? 0;

        // a change while a line is sung is marked before the line or after it, by where most of it is: the line is not cut
        var sung = rows.OfType<WordsRow>().Select(x => (x.Line.Start, x.Line.End)).ToList();
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            if (middles[r] is { } middle)
            {
                // the changes between this row and the one before, each in its place among the headers (a bar of
                // another meter where nothing begins is marked after the row before it)
                foreach (var change in song.Changes.Where(x => since < x && x < middle && !sung.Any(l => l.Start < x && x < l.End)).Order())
                {
                    Headers(change);
                    var changed = song.StateAt(change + Near);
                    if (changed.Mark(state) is { } between) lines.Add([between]);
                    state = changed;
                }

                since = middle;
                Headers(middle);
                var newState = song.StateAt(middle);
                if (newState.Mark(state) is { } mark) lines.Add([mark]);
                state = newState;
            }

            switch (row)
            {
                case ChordsRow chordsRow:
                    lines.Add(ChordLine(chordsRow.Chords));
                    break;
                case WordsRow wordsRow:
                    if (wordsRow.Chords.Count > 0)
                    {
                        lines.Add(ChordLine(wordsRow.Chords));
                    }
                    else if (previous is ChordsRow && lines[^1].OfType<NodeChord>().Any())
                    {
                        // The chords alone above would otherwise be drawn over these words.
                        lines.Add([]);
                    }

                    lines.Add(WordsLine(wordsRow.Words));
                    break;
                case TextRow textRow:
                    if (previous is ChordsRow && lines[^1].OfType<NodeChord>().Any()) lines.Add([]);
                    lines.Add([new NodeText(textRow.Text)]);
                    break;
            }

            previous = row;
        }

        // the sections after the last row
        Headers(double.PositiveInfinity);

        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0) sheet.Children.Add(new NodeText("\n").AsChild());
            sheet.Children.AddRange(lines[i].Select(x => x.AsChild()));
        }

        return sheet;
    }

    private static List<NodeBase> ChordLine(IReadOnlyList<(ChordEvent chord, int column)> chords)
    {
        var nodes = new List<NodeBase>();
        var at = 0;
        foreach (var (chord, column) in chords)
        {
            if (column > at) nodes.Add(new NodeText(new string(' ', column - at)));
            nodes.Add(new NodeChord
            {
                Children = { new NodeText(chord.Name).AsChild() },
                Degree = chord.Degree,
                BassDegree = chord.BassDegree,
                Time = chord.Time,
            });
            at = column + chord.Name.Length;
        }

        return nodes;
    }

    private static List<NodeBase> WordsLine(IReadOnlyList<(Word word, int column)> words)
    {
        var nodes = new List<NodeBase>();
        var at = 0;
        foreach (var (word, column) in words)
        {
            if (column > at) nodes.Add(new NodeText(new string(' ', column - at)));
            nodes.Add(new NodeText(word.Text) { Time = word.OwnTime });
            at = column + word.Text.Length;
        }

        return nodes;
    }

    // ------------------------------------------------------------------ the song: its keys, bars and sections

    /// <summary>
    /// The key, the meter and the tempo in force at a moment.
    /// </summary>
    private sealed record State(string? Key, string? TimeSignature, Tempo? Tempo, double KeySince, double TimeSignatureSince, double TempoSince)
    {
        /// <summary>
        /// The mark of what changed since <paramref name="before"/> (everything known, at the start); null when nothing
        /// did. A meter that is not a number of beats ("free") has no mark: the meter after it is marked again.
        /// </summary>
        public NodeMark? Mark(State? before)
        {
            var key = Key != null && (before == null || before.Key != Key) ? SheetKey.Parse(Key) : null;
            var timeSignature = TimeSignature != null && (before == null || before.TimeSignature != TimeSignature) ? ParseTimeSignature(TimeSignature) : null;
            var tempo = Tempo != null && (before == null || before.Tempo != Tempo)
                ? new SheetTempo(Tempo.Bpm, Tempo.BeatUnit.Denominator, Tempo.BeatUnit.Dotted)
                : null;

            if (key == null && timeSignature == null && tempo == null)
                return null;

            var since = new[] { (key != null, KeySince), (timeSignature != null, TimeSignatureSince), (tempo != null, TempoSince) }
                .Where(x => x.Item1)
                .Max(x => x.Item2);

            return new() { Key = key, TimeSignature = timeSignature, Tempo = tempo, Time = since };
        }
    }

    /// <summary>
    /// "3/4", "6/8"; a sum of groups as the number of its notes ("2+2+3/8" is 7/8); "free" has none.
    /// </summary>
    private static SheetTimeSignature? ParseTimeSignature(string text)
    {
        if (text == "free") return null;
        var parts = text.Split('/');
        if (parts.Length != 2) throw new ArgumentOutOfRangeException(nameof(text), text, "A meter like 3/4, 2+2+3/8 or free is expected.");
        return new(parts[0].Split('+').Sum(int.Parse), int.Parse(parts[1]));
    }

    private sealed class Song(Timeline timeline)
    {
        public IReadOnlyList<KeySpan> Keys => timeline.Keys;

        public double Duration => timeline.Duration;

        /// <summary>
        /// When the first chord sounds; null when there is none.
        /// </summary>
        public double? FirstChord => timeline.Chords.FirstOrDefault(x => x.Symbol.Kind == ChordSymbolKind.Chord)?.Start;

        public IReadOnlyList<Section> Sections => timeline.Sections;

        public IReadOnlyList<double> SectionStarts { get; } = timeline.Sections.Select(x => x.Start).ToList();

        /// <summary>
        /// Where the key, the meter or the tempo changes.
        /// </summary>
        public IReadOnlyList<double> Changes { get; } = ChangesOf(timeline);

        /// <summary>
        /// Where a section begins, or the key, the meter or the tempo changes.
        /// </summary>
        public IReadOnlyList<double> Breaks { get; } = timeline.Sections.Select(x => x.Start).Concat(ChangesOf(timeline)).ToList();

        private static IReadOnlyList<double> ChangesOf(Timeline timeline) => timeline.Keys.Skip(1).Select(x => x.Start)
            .Concat(timeline.Bars.Skip(1).Select((bar, i) => (bar, before: timeline.Bars[i]))
                .Where(x => x.bar.TimeSignature != x.before.TimeSignature || x.bar.Tempo != x.before.Tempo)
                .Select(x => x.bar.Start))
            .ToList();

        public IReadOnlyList<ChordEvent> ChordEvents()
        {
            var events = new List<ChordEvent>();
            foreach (var chord in timeline.Chords)
            {
                if (chord.Symbol.Kind != ChordSymbolKind.Chord)
                {
                    // Before the first chord nothing sounds; a run of silences is written once.
                    if (events.Count > 0 && events[^1].Name != NoChord)
                        events.Add(new(chord.Start, NoChord, null, null));
                    continue;
                }

                var name = chord.Symbol.Name ?? throw new UnreadableChordException(string.Empty);
                var (root, bass) = ChordReading.Read(name);
                // the degree as the material gives it; worked out in the key where the chord stands when it gives none
                var key = KeyAt(chord.Start);
                var degrees = ChordReading.GivenDegree(chord.Degree, bass != null)
                              ?? (key == null ? ((string?)null, (string?)null) : (ChordReading.Degree(root, key), bass == null ? null : ChordReading.Degree(bass, key)));
                events.Add(new(chord.Start, name, degrees.Item1, degrees.Item2));
            }

            return events;
        }

        private SheetKey? KeyAt(double time)
        {
            var span = timeline.Keys.LastOrDefault(x => x.Start <= time + Near) ?? timeline.Keys.FirstOrDefault();
            return span?.Key == null ? null : SheetKey.Parse(span.Key);
        }

        /// <summary>
        /// The bar sounding at the moment (the first, before it); null when there are no bars.
        /// </summary>
        public int? BarAt(double time)
        {
            if (timeline.Bars.Count == 0) return null;
            var i = 0;
            while (i + 1 < timeline.Bars.Count && timeline.Bars[i + 1].Start <= time) i++;
            return i;
        }

        public State StateAt(double time)
        {
            var keyIndex = timeline.Keys.Count == 0 ? (int?)null : Math.Max(0, timeline.Keys.ToList().FindLastIndex(x => x.Start <= time));
            var key = keyIndex.HasValue ? timeline.Keys[keyIndex.Value] : null;

            if (BarAt(time) is not { } bar)
                return new(key?.Key, null, null, key?.Start ?? 0, 0, 0);

            // since when the meter and the tempo of this bar are in force
            var bars = timeline.Bars;
            var meterSince = bar;
            while (meterSince > 0 && bars[meterSince - 1].TimeSignature == bars[bar].TimeSignature) meterSince--;
            var tempoSince = bar;
            while (tempoSince > 0 && bars[tempoSince - 1].Tempo == bars[bar].Tempo) tempoSince--;

            return new(key?.Key, bars[bar].TimeSignature, bars[bar].Tempo, key?.Start ?? 0, bars[meterSince].Start, bars[tempoSince].Start);
        }
    }
}
