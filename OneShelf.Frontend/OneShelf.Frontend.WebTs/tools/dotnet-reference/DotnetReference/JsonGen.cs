using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using HarmonyDB.Common.Representations.OneShelf;

namespace DotnetReference;

/// <summary>Random JSON for the model types, made from System.Text.Json's own contracts of them (property names under
/// either naming, required members, member types), so that both readers are fed what C# itself would accept.</summary>
public static class JsonGen
{
    private static readonly string[] Words =
    [
        "", " ", "a", "Am", "кино", "Ёлка", "\u0000", "\u001f", "\t\r\n", "\"quoted\"", "back\\slash", "<b>&amp;</b>", "'+`",
        "é", "😀", "\ud83d", "\ude00x", new string([(char)0x2028, (char)0x2029]), "\u0085", ((char)0xFEFF).ToString(), "ß", "İ", "ǅ", "1.5E2", "null", "Zz",
    ];

    public static IEnumerable<JsonObject> Generate(int seed, int countPerType)
    {
        var random = new Random(seed);
        var n = 0;
        foreach (var (name, type) in Suites.ModelTypes.Where(x => TypeNames.Contains(x.Key)).OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            for (var i = 0; i < countPerType; i++)
            {
                var camel = i % 2 == 0;
                var options = camel ? Suites.Camel : Suites.Pascal;
                var node = Value(type, options, random, 0);
                yield return new JsonObject
                {
                    ["id"] = $"json-gen-{n++}",
                    ["suite"] = "json",
                    ["input"] = new JsonObject
                    {
                        ["type"] = name,
                        ["json"] = node?.ToJsonString() ?? "null",
                        ["naming"] = camel ? "camel" : "pascal",
                    },
                };
            }
        }
    }

    /// <summary>The types the TypeScript ports have contracts for.</summary>
    public static readonly HashSet<string> TypeNames =
    [
        "HarmonyDB.Common.Representations.OneShelf.NodeChild",
        "HarmonyDB.Common.Representations.OneShelf.NodeChord",
        "HarmonyDB.Common.Representations.OneShelf.NodeHtml",
        "HarmonyDB.Common.Representations.OneShelf.NodeLine",
        "HarmonyDB.Common.Representations.OneShelf.NodeNote",
        "HarmonyDB.Common.Representations.OneShelf.NodeText",
        "HarmonyDB.Common.Representations.OneShelf.Note",
        "HarmonyDB.Source.Api.Model.V1.Api.GetSongsRequest",
        "HarmonyDB.Source.Api.Model.V1.Api.GetSongsResponse",
        "HarmonyDB.Source.Api.Model.V1.Api.SearchRequest",
        "HarmonyDB.Source.Api.Model.V1.Api.SearchResponse",
        "HarmonyDB.Source.Api.Model.V1.Chords",
        "HarmonyDB.Source.Api.Model.V1.SearchHeader",
        "OneShelf.Authorization.Api.Model.Identity",
        "OneShelf.Collectives.Api.Model.V2.DeleteRequest",
        "OneShelf.Collectives.Api.Model.V2.DeleteResponse",
        "OneShelf.Collectives.Api.Model.V2.GetRequest",
        "OneShelf.Collectives.Api.Model.V2.GetResponse",
        "OneShelf.Collectives.Api.Model.V2.InsertRequest",
        "OneShelf.Collectives.Api.Model.V2.InsertResponse",
        "OneShelf.Collectives.Api.Model.V2.Sub.Collective",
        "OneShelf.Collectives.Api.Model.V2.Sub.CollectiveVersion",
        "OneShelf.Collectives.Api.Model.V2.UpdateRequest",
        "OneShelf.Collectives.Api.Model.V2.UpdateResponse",
        "OneShelf.Frontend.Api.Model.V3.Api.ApplyRequest",
        "OneShelf.Frontend.Api.Model.V3.Api.ApplyResponse",
        "OneShelf.Frontend.Api.Model.V3.Api.CreateLikeCategoryRequest",
        "OneShelf.Frontend.Api.Model.V3.Api.CreateLikeCategoryResponse",
        "OneShelf.Frontend.Api.Model.V3.Api.DeleteLikeCategoryRequest",
        "OneShelf.Frontend.Api.Model.V3.Api.DeleteLikeCategoryResponse",
        "OneShelf.Frontend.Api.Model.V3.Api.FormatPreviewRequest",
        "OneShelf.Frontend.Api.Model.V3.Api.FormatPreviewResponse",
        "OneShelf.Frontend.Api.Model.V3.Api.GetPdfsRequest",
        "OneShelf.Frontend.Api.Model.V3.Api.GetPdfsRequestVersion",
        "OneShelf.Frontend.Api.Model.V3.Api.GetPdfsResponse",
        "OneShelf.Frontend.Api.Model.V3.Api.GetPdfsResponse+Pdf",
        "OneShelf.Frontend.Api.Model.V3.Api.GetVisitedChordsRequest",
        "OneShelf.Frontend.Api.Model.V3.Api.GetVisitedChordsResponse",
        "OneShelf.Frontend.Api.Model.V3.Api.GetVisitedSearchesRequest",
        "OneShelf.Frontend.Api.Model.V3.Api.GetVisitedSearchesResponse",
        "OneShelf.Frontend.Api.Model.V3.Api.IllustrationsRequest",
        "OneShelf.Frontend.Api.Model.V3.Api.IllustrationsResponse",
        "OneShelf.Frontend.Api.Model.V3.Api.UpdateLikeCategoryRequest",
        "OneShelf.Frontend.Api.Model.V3.Api.UpdateLikeCategoryResponse",
        "OneShelf.Frontend.Api.Model.V3.Databasish.Artist",
        "OneShelf.Frontend.Api.Model.V3.Databasish.Collection",
        "OneShelf.Frontend.Api.Model.V3.Databasish.Like",
        "OneShelf.Frontend.Api.Model.V3.Databasish.LikeCategory",
        "OneShelf.Frontend.Api.Model.V3.Databasish.Song",
        "OneShelf.Frontend.Api.Model.V3.Databasish.User",
        "OneShelf.Frontend.Api.Model.V3.Databasish.Version",
        "OneShelf.Frontend.Api.Model.V3.Illustrations.AllIllustrations",
        "OneShelf.Frontend.Api.Model.V3.Illustrations.ImagePublicUrls",
        "OneShelf.Frontend.Api.Model.V3.Illustrations.SongIllustration",
        "OneShelf.Frontend.Api.Model.V3.Illustrations.SongIllustrations",
        "OneShelf.Frontend.Api.Model.V3.Instant.ImportedVersion",
        "OneShelf.Frontend.Api.Model.V3.Instant.RemovedVersion",
        "OneShelf.Frontend.Api.Model.V3.Instant.UpdatedLike",
        "OneShelf.Frontend.Api.Model.V3.Instant.VisitedChords",
        "OneShelf.Frontend.Api.Model.V3.Instant.VisitedSearch",
    ];

    private static string RandomString(Random random)
    {
        var builder = new StringBuilder();
        var parts = random.Next(0, 4);
        for (var i = 0; i < parts; i++)
        {
            builder.Append(random.Next(3) == 0 ? ((char)random.Next(0x20, 0x7f)).ToString() : Words[random.Next(Words.Length)]);
        }

        return builder.ToString();
    }

    private static JsonNode Number(Random random, bool single)
    {
        return random.Next(8) switch
        {
            0 => JsonValue.Create(0),
            1 => JsonValue.Create(random.Next(-1000, 1000)),
            2 => JsonValue.Create(Math.Round(random.NextDouble() * 10, random.Next(1, 4))),
            3 => single ? JsonValue.Create((double)(float)(random.NextDouble() * 1e6)) : JsonValue.Create(random.NextDouble() * 1e6),
            4 => JsonValue.Create(random.NextDouble() * 1e-6),
            5 => single ? JsonValue.Create(float.MaxValue) : JsonValue.Create(1e300 * random.NextDouble()),
            6 => JsonValue.Create(-random.NextDouble()),
            _ => JsonValue.Create(0.1 * random.Next(1, 30)),
        };
    }

    private static string RandomDate(Random random)
    {
        var date = new DateTime(2000, 1, 1).AddSeconds(random.NextDouble() * 1e9).AddTicks(random.Next(0, 10_000_000));
        var text = date.ToString(random.Next(3) switch
        {
            0 => "yyyy-MM-ddTHH:mm:ss",
            1 => "yyyy-MM-ddTHH:mm:ss.fffffff",
            _ => "yyyy-MM-ddTHH:mm:ss.fff",
        }, System.Globalization.CultureInfo.InvariantCulture);
        return text + random.Next(4) switch
        {
            0 => "",
            1 => "Z",
            2 => "+03:00",
            _ => "-05:30",
        };
    }

    private static JsonNode? Value(Type type, JsonSerializerOptions options, Random random, int depth)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            return random.Next(4) == 0 ? null : Value(underlying, options, random, depth);
        }

        if (type == typeof(string)) return random.Next(10) == 0 ? null : JsonValue.Create(RandomString(random));
        if (type == typeof(bool)) return JsonValue.Create(random.Next(2) == 0);
        if (type == typeof(int)) return JsonValue.Create(random.Next(6) == 0 ? (random.Next(2) == 0 ? int.MaxValue : int.MinValue) : random.Next(-100, 100000));
        if (type == typeof(long)) return JsonValue.Create(random.Next(6) == 0 ? 9007199254740991L * (random.Next(2) == 0 ? 1 : -1) : random.NextInt64(-100, 10_000_000_000));
        if (type == typeof(byte)) return JsonValue.Create(random.Next(256));
        if (type == typeof(short)) return JsonValue.Create(random.Next(short.MinValue, short.MaxValue));
        if (type == typeof(float)) return Number(random, true);
        if (type == typeof(double) || type == typeof(decimal)) return Number(random, false);
        if (type == typeof(DateTime)) return JsonValue.Create(RandomDate(random));
        if (type == typeof(Guid))
        {
            var guid = new Guid(Enumerable.Range(0, 16).Select(_ => (byte)random.Next(256)).ToArray()).ToString();
            return JsonValue.Create(random.Next(3) == 0 ? guid.ToUpperInvariant() : guid);
        }

        if (type == typeof(Uri)) return JsonValue.Create(random.Next(3) switch { 0 => "https://example.com/a?b=c#d", 1 => "/relative/path", _ => "https://пример.рф/путь" });
        if (type == typeof(byte[])) return random.Next(5) == 0 ? null : JsonValue.Create(Enumerable.Range(0, random.Next(0, 7)).Select(_ => (byte)random.Next(256)).ToArray());
        if (type.IsEnum)
        {
            var values = Enum.GetValues(type).Cast<object>().Select(Convert.ToInt32).ToList();
            return JsonValue.Create(random.Next(8) == 0 ? 77 : values[random.Next(values.Count)]);
        }

        if (type == typeof(object))
        {
            return random.Next(5) switch
            {
                0 => JsonValue.Create(RandomString(random)),
                1 => JsonValue.Create(random.Next(2) == 0),
                2 => JsonNode.Parse(random.Next(2) == 0 ? "1.50" : "-0.0e+3"),
                3 => new JsonObject { ["k"] = new JsonArray(1, "x", null) },
                _ => null,
            };
        }

        var dictionaryInterface = type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Dictionary<,>) || type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>) || type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            ? type
            : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        if (dictionaryInterface != null)
        {
            if (random.Next(8) == 0) return null;
            var keyType = dictionaryInterface.GetGenericArguments()[0];
            var valueType = dictionaryInterface.GetGenericArguments()[1];
            var result = new JsonObject();
            var count = depth > 3 ? 0 : random.Next(0, 4);
            for (var i = 0; i < count; i++)
            {
                var key = keyType == typeof(string) ? RandomString(random) + i : (random.Next(4) == 0 ? -random.Next(1, 1000) : random.Next(0, 1_000_000)).ToString();
                if (result.ContainsKey(key)) continue;
                result[key] = Value(valueType, options, random, depth + 1);
            }

            return result;
        }

        var enumerable = type.IsArray
            ? type.GetElementType()
            : type.IsGenericType && type.GetGenericArguments().Length == 1 && typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
                ? type.GetGenericArguments()[0]
                : null;
        if (enumerable != null)
        {
            if (random.Next(10) == 0) return null;
            var count = depth > 3 ? 0 : random.Next(0, 4);
            return new JsonArray(Enumerable.Range(0, count).Select(_ => Value(enumerable, options, random, depth + 1)).ToArray());
        }

        var info = options.GetTypeInfo(type);
        if (info.Kind != JsonTypeInfoKind.Object) throw new NotSupportedException(type.FullName);
        if (depth > 0 && random.Next(12) == 0) return null;

        var obj = new JsonObject();
        // NodeChild: one member set, as the original writes it (the others are skipped when null).
        var oneOf = type == typeof(NodeChild) ? info.Properties[random.Next(depth > 4 ? 2 : info.Properties.Count)].Name : null;
        foreach (var property in info.Properties)
        {
            if (oneOf != null && property.Name != oneOf) continue;
            var include = property.IsRequired || property.PropertyType == typeof(Guid) || oneOf != null || random.Next(10) < 8;
            if (!include) continue;
            obj[property.Name] = Value(property.PropertyType, options, random, depth + 1);
        }

        if (random.Next(10) == 0) obj["unknownMember"] = new JsonArray(1, new JsonObject { ["x"] = "y" });
        return obj;
    }
}
