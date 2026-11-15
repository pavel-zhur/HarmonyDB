using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using HarmonyDB.Common;
using HarmonyDB.Common.FullTextSearch;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Common.Transposition;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Tools;
using OneShelf.Common;
using OneShelf.Common.Songs;
using OneShelf.Common.Songs.FullTextSearch;
using OneShelf.Common.Songs.Hashing;
using static DotnetReference.J;

namespace DotnetReference;

public static partial class Suites
{
    private static (byte root, bool isMinor)? Tonality(JsonElement e, string name)
        => e.Has(name) ? ((byte)e.GetProperty(name)[0].GetInt32(), e.GetProperty(name)[1].GetBoolean()) : null;

    private static JsonNode Tonality((byte root, bool isMinor) t) => A(I(t.root), B(t.isMinor));

    private static JsonNode Tonal(JsonElement input)
    {
        var probabilities = input.Arr("p").Select(x => (float)x.Dbl()).ToArray();
        var predicted = Tonality(input, "pred");
        var tonality = Tonality(input, "ton")!.Value;
        var beginningNote = (byte)input.Int("bn");
        var normalizationRoot = (byte?)input.IntN("nr");
        var result = new JsonObject
        {
            ["pred"] = Try(() => A(Tonality(probabilities.GetPredictedTonality(out var c)), D(c))),
            ["second"] = Try(() => A(Tonality(probabilities.GetSecondPredictedTonality(out var c)), D(c))),
            ["conf"] = Try(() => D(probabilities.TonalityConfidence())),
            ["tonic"] = Try(() => A(D(probabilities.TonicConfidence(true)), D(probabilities.TonicConfidence(false)))),
            ["linear"] = Try(() =>
            {
                var array = new float[12, 2];
                for (var i = 0; i < 24 && i < probabilities.Length; i++) array[i / 2, i % 2] = probabilities[(i * 7) % probabilities.Length];
                return A(array.ToLinear().Select(x => (JsonNode?)D(x)));
            }),
            ["scale"] = A(
                I(tonality.GetMajorTonic(true)), I(tonality.GetMajorTonic(false)),
                Tonality(tonality.GetRelativeScale(true)), Tonality(tonality.GetRelativeScale(false)),
                I(tonality.GetAmCTonic()), Tonality(tonality.GetAmCRoot()),
                I(tonality.ToIndex()),
                S(tonality.ToSongTonalityTitle()),
                S(tonality.ToLoopTonalityTitle()),
                Try(() => S(tonality.ToLoopTonalityTitle(normalizationRoot))),
                Try(() => S(tonality.ToLoopTonalityTitle(predicted)))),
            ["order"] = A(TonalitiesExtensions.GetLoopTonalityShiftsDisplayOrder(predicted).Select(x => (JsonNode?)I(x))),
            ["shifts"] = A(Enumerable.Range(0, 14).Select(x => Try(() => S(((byte)x).ToLoopTonalityShiftTitle())))),
            ["indices"] = A(input.Arr("idx").Select(x => x.GetInt32()).Select(i => (JsonNode?)A(
                Try(() => Tonality(i.FromIndex())),
                Try(() => S(i.ToSongTonalityTitle())),
                Try(() => S(i.ToLoopTonalityTitle(predicted)))))),
            ["parsed"] = A(input.Strings("strs").Select(s => Try(() => s.TryParseBestTonality() is { } t ? Tonality(t) : null))),
            ["chords"] = A(input.Arr("chords").Select(c => ((byte)c[0].GetInt32(), (ChordType)c[1].GetInt32())).Select(c => (JsonNode?)A(
                Try(() => S(c.ToChord())),
                Try(() => S(c.ToFunction(tonality)))))),
            ["weights"] = A(input.Arr("w").Select(w => Try(() => D(TonalitiesExtensions.GetWeight((float)w[0].Dbl(), (float)w[1].Dbl(), w[2].GetInt32(), w[3].GetBoolean()))))),
            ["titles"] = A(input.Strings("loops").Select(normalized => (JsonNode?)A(
                Try(() => Movements(normalized.DeserializeLoop())),
                Try(() => S(normalized.GetTitle(predicted))),
                Try(() => S(normalized.GetTitle(predicted, true))),
                Try(() => S(normalized.GetTitle(predicted, true, false))),
                Try(() => S(normalized.GetTitle(beginningNote))),
                Try(() => S(normalized.GetTitle(beginningNote, true))),
                Try(() => S(normalized.GetTitle(tonality, beginningNote))),
                Try(() => S(normalized.GetTitle(tonality, beginningNote, true))),
                Try(() => S(normalized.GetFunctionsTitle(tonality))),
                Try(() => S(normalized.GetFunctionsTitle(tonality, true))),
                Try(() => S(normalized.GetFunctionsTitle(tonality, true, false))),
                Try(() => S(normalized.DeserializeLoop().GetTitle(predicted, beginningNote, true, true))),
                Try(() => A(normalized.DeserializeLoop().ToSequence(predicted, beginningNote, out var shift, true).Select(x => (JsonNode?)A(I(x.note), I((int)x.chordType))).Append(I(shift)))),
                Try(() => S(normalized.DeserializeLoop().SerializeLoop()))))),
        };
        return result;
    }

    private class SearchableLike(byte level, long userId) : ILike
    {
        public byte Level { get; } = level;
        public long UserId { get; } = userId;
    }

    private class SearchableArtist(string name, List<string> synonyms) : ISearchableArtist
    {
        public string Name { get; } = name;
        public IEnumerable<string> Synonyms => synonyms;
    }

    private class SearchableSong(string title, string? additionalKeywords, int index, List<ILike> likes, List<SearchableArtist> artists) : ISearchableSong
    {
        public string Title { get; } = title;
        public string? AdditionalKeywords { get; } = additionalKeywords;
        public int Index { get; } = index;
        public IEnumerable<ILike> Likes => likes;
        public List<SearchableArtist> Artists { get; } = artists;
    }

    private static JsonNode Fts(JsonElement input)
    {
        var songs = input.Arr("songs").Select(s => new SearchableSong(
            s.Str("title")!,
            s.Str("kw"),
            s.Int("index"),
            s.Arr("likes").Select(l => (ILike)new SearchableLike((byte)l[1].GetInt32(), l[0].GetInt64())).ToList(),
            s.Arr("artists").Select(a => new SearchableArtist(a.Str("name")!, a.Strings("syn"))).ToList())).ToList();

        var result = new JsonObject
        {
            ["ratings"] = A(songs.Select(s => Try(() => D(s.Likes.GetRating())))),
            ["fragments"] = A(input.Arr("frag").Select(f => Try(() => A(SearchHighlightingTools.GetFragments(f[0].ValueKind == JsonValueKind.Null ? null : f[0].GetString(), f[1].GetString()!)
                .Select(x => (JsonNode?)A(S(x.fragment), B(x.isHighlighted))))))),
            ["syntax"] = A(input.Strings("queries").Select(q => (JsonNode?)A(S(q.ToSearchSyntax()), S(q.SearchSyntaxRemoveSeparators()), B(q.SearchSyntaxAnySeparatorsToRemove())))),
        };

        IReadOnlyCollection<(SearchableSong song, IReadOnlyCollection<string> words)> cache;
        try
        {
            cache = FullTextSearchLogic.BuildCache(songs, s => s.Artists);
            result["cache"] = A(cache.Select(x => (JsonNode?)A(I(IndexOfRef(songs, x.song)), A(x.words.Select(S)))));
        }
        catch (Exception e)
        {
            result["cache"] = Error(e);
            return result;
        }

        result["find"] = A(input.Strings("queries").SelectMany(q => input.Arr("users").Select(u => Try(() =>
        {
            var (found, isUserSpecific) = FullTextSearchLogic.Find(cache, q, u.GetInt64());
            return A(A(found.Select(s => (JsonNode?)I(IndexOfRef(songs, s)))), B(isUserSpecific));
        }))));

        return result;
    }

    private static JsonNode Dotnet(JsonElement input)
    {
        var f = input.Str("f")!;
        switch (f)
        {
            case "sort":
            {
                var items = input.Arr("keys").Select((k, i) => (key: k.GetInt32(), i)).ToList();
                items.Sort((a, b) => a.key.CompareTo(b.key));
                return A(items.Select(x => (JsonNode?)I(x.i)));
            }
            case "sortStrings":
            {
                var items = input.Strings("keys").Select((k, i) => (key: k, i)).ToList();
                var byDefault = items.ToList();
                byDefault.Sort((a, b) => Comparer<string>.Default.Compare(a.key, b.key));
                var orderBy = items.OrderBy(x => x.key).ToList();
                return A(A(byDefault.Select(x => (JsonNode?)I(x.i))), A(orderBy.Select(x => (JsonNode?)I(x.i))));
            }
            case "pq":
            {
                var queue = new PriorityQueue<int, int>();
                var result = new List<JsonNode?>();
                var next = 0;
                foreach (var op in input.Arr("ops"))
                {
                    if (op.ValueKind == JsonValueKind.Number)
                    {
                        queue.Enqueue(next++, op.GetInt32());
                    }
                    else
                    {
                        result.Add(Try(() => I(queue.Dequeue())));
                    }
                }

                while (queue.Count > 0) result.Add(I(queue.Dequeue()));
                return A(result);
            }
            case "pqFloat":
            {
                var queue = new PriorityQueue<int, float>();
                var result = new List<JsonNode?>();
                var next = 0;
                foreach (var op in input.Arr("ops"))
                {
                    if (op.ValueKind == JsonValueKind.Array)
                    {
                        queue.Enqueue(next++, (float)op[0].Dbl());
                    }
                    else
                    {
                        result.Add(Try(() => I(queue.Dequeue())));
                    }
                }

                while (queue.Count > 0) result.Add(I(queue.Dequeue()));
                return A(result);
            }
            case "format":
            {
                var value = input.GetProperty("v").Dbl();
                var single = (float)value;
                var decimals = input.Int("d");
                return A(
                    S(value.ToString("N" + decimals)),
                    S(value.ToString("P" + decimals)),
                    S(single.ToString("N" + decimals)),
                    S(single.ToString("P" + decimals)),
                    D(single),
                    Try(() => S(JsonSerializer.Serialize(value))),
                    Try(() => S(JsonSerializer.Serialize(single))),
                    I(single.GetHashCode()),
                    Math.Abs(value) < 4e18 ? I(((long)Math.Truncate(value)).GetHashCode()) : null);
            }
            case "readNumber":
            {
                var text = input.Str("txt")!;
                return A(
                    Try(() => D(JsonSerializer.Deserialize<double>(text))),
                    Try(() => D(JsonSerializer.Deserialize<float>(text))),
                    Try(() => I(JsonSerializer.Deserialize<int>(text))),
                    Try(() => I(JsonSerializer.Deserialize<long>(text))),
                    Try(() => I(JsonSerializer.Deserialize<byte>(text))),
                    Try(() => S(JsonSerializer.Deserialize<NoteAlteration?>(text)?.ToString())));
            }
            case "chars":
            {
                var whiteSpace = new List<JsonNode?>();
                var upper = new List<JsonNode?>();
                var digit = new List<JsonNode?>();
                var lower = new List<JsonNode?>();
                var toUpper = new List<JsonNode?>();
                var regexDigit = new List<JsonNode?>();
                var regexAny = new List<JsonNode?>();
                var digitRegex = new System.Text.RegularExpressions.Regex("^\\d$");
                var anyRegex = new System.Text.RegularExpressions.Regex("^.$");
                for (var i = 0; i < 0x10000; i++)
                {
                    var c = (char)i;
                    if (char.IsWhiteSpace(c)) whiteSpace.Add(I(i));
                    if (char.IsUpper(c)) upper.Add(I(i));
                    if (char.IsDigit(c)) digit.Add(I(i));
                    if (char.ToLowerInvariant(c) != c) lower.Add(A(I(i), I(char.ToLowerInvariant(c))));
                    if (char.ToUpperInvariant(c) != c) toUpper.Add(A(I(i), I(char.ToUpperInvariant(c))));
                    if (digitRegex.IsMatch(c.ToString())) regexDigit.Add(I(i));
                    if (!anyRegex.IsMatch(c.ToString())) regexAny.Add(I(i));
                }

                // string.ToLowerInvariant / ToUpperInvariant of every code point (the ones outside the BMP as surrogate pairs).
                var stringLower = new List<JsonNode?>();
                var stringUpper = new List<JsonNode?>();
                for (var cp = 0; cp <= 0x10FFFF; cp++)
                {
                    if (cp is >= 0xD800 and <= 0xDFFF) continue;
                    var s = char.ConvertFromUtf32(cp);
                    var lowerString = s.ToLowerInvariant();
                    var upperString = s.ToUpperInvariant();
                    if (lowerString != s) stringLower.Add(A(I(cp), I(char.ConvertToUtf32(lowerString, 0))));
                    if (upperString != s) stringUpper.Add(A(I(cp), I(char.ConvertToUtf32(upperString, 0))));
                }

                return O(("ws", A(whiteSpace)), ("up", A(upper)), ("dg", A(digit)), ("lo", A(lower)), ("tu", A(toUpper)), ("rd", A(regexDigit)), ("notAny", A(regexAny)),
                    ("slo", A(stringLower)), ("stu", A(stringUpper)));
            }
            case "str":
            {
                var s = input.Str("s")!;
                var t = input.Str("t") ?? string.Empty;
                return O(
                    ("trim", S(s.Trim())),
                    ("ws", B(string.IsNullOrWhiteSpace(s))),
                    ("lower", S(s.ToLowerInvariant())),
                    ("upper", S(s.ToUpperInvariant())),
                    ("split0", A(s.Split(FullTextSearchExtensions.Separators).Select(S))),
                    ("split1", A(s.Split(FullTextSearchExtensions.Separators, StringSplitOptions.RemoveEmptyEntries).Select(S))),
                    ("split3", A(s.Split(FullTextSearchExtensions.Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(S))),
                    ("syntax", S(s.ToSearchSyntax())),
                    ("json", S(JsonSerializer.Serialize(s))),
                    ("jsonBack", Try(() => S(JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(s))))),
                    ("camel", S(JsonNamingPolicy.CamelCase.ConvertName(s))),
                    ("int", Try(() => int.TryParse(s, out var v) ? I(v) : null)),
                    ("intTrim", Try(() => int.TryParse(s.Trim(), out var v) ? I(v) : null)),
                    ("byte", Try(() => I(byte.Parse(s)))),
                    ("enum", Try(() => S(Enum.Parse<NoteAlteration>(s).ToString()))),
                    ("html", S(HttpUtility.HtmlEncode(s))),
                    ("attr", S(HttpUtility.HtmlAttributeEncode(s))),
                    ("startsWith", B(s.StartsWith(t))),
                    ("contains", B(s.Contains(t))),
                    ("indexOf", I(s.IndexOf(t, StringComparison.Ordinal))),
                    ("compare", I(Math.Sign(string.Compare(s, t)))),
                    ("compareOrdinal", I(Math.Sign(string.CompareOrdinal(s, t)))),
                    ("containsIc", B(s.Contains(t, StringComparison.InvariantCultureIgnoreCase))),
                    ("startsWithIc", B(s.StartsWith(t, StringComparison.InvariantCultureIgnoreCase))),
                    ("replaceIc", Try(() => S(s.Replace(t, "<>", StringComparison.InvariantCultureIgnoreCase)))),
                    ("transposition", Try(() => int.TryParse(s, out var v) ? S(v.Transposition()) : null)),
                    ("b64", Try(() => S(Convert.ToHexString(Convert.FromBase64String(s))))),
                    ("b64loop", Try(() => S(s.DeserializeLoop().SerializeLoop()))),
                    ("guid", Try(() => S(JsonSerializer.Deserialize<Guid>(JsonSerializer.Serialize(s)).ToString()))),
                    ("date", Try(() =>
                    {
                        var date = JsonSerializer.Deserialize<DateTime>(JsonSerializer.Serialize(s));
                        return A(S(date.Ticks.ToString(CultureInfo.InvariantCulture)), I((int)date.Kind), S(JsonSerializer.Serialize(date)),
                            S(JsonSerializer.Serialize(date.ToUniversalTime())), S(JsonSerializer.Serialize(date.ToLocalTime())), I(date.GetHashCode()));
                    })));
            }
            case "bytes":
            {
                var bytes = input.Arr("b").Select(b => (byte)b.GetInt32()).ToArray();
                return A(S(Convert.ToBase64String(bytes)), S(JsonSerializer.Serialize(bytes)));
            }
            case "hash":
            {
                return A(
                    I(MyHashExtensions.CalculateHash(input.Arr("v").Select(x => x.GetInt32()))),
                    A(input.Arr("v").Select(x => (JsonNode?)S(x.GetInt32().Transposition()))));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(f), f, null);
        }
    }
}
