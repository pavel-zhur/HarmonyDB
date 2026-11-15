using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DotnetReference;

/// <summary>The neutral output form. Numbers are doubles (a float is written as the double it converts to);
/// NaN and infinities are the strings "NaN", "Infinity", "-Infinity"; an exception is {"$error": its type name}.</summary>
public static class J
{
    public static JsonNode? S(string? value) => value == null ? null : JsonValue.Create(value);

    public static JsonNode I(long value) => JsonValue.Create(value);

    public static JsonNode? I(long? value) => value.HasValue ? JsonValue.Create(value.Value) : null;

    public static JsonNode D(double value) => double.IsFinite(value)
        ? JsonValue.Create(value)
        : JsonValue.Create(double.IsNaN(value) ? "NaN" : value > 0 ? "Infinity" : "-Infinity");

    public static JsonNode B(bool value) => JsonValue.Create(value);

    public static JsonNode? B(bool? value) => value.HasValue ? JsonValue.Create(value.Value) : null;

    public static JsonArray A(IEnumerable<JsonNode?> items) => new(items.ToArray());

    public static JsonArray A(params JsonNode?[] items) => new(items);

    public static JsonObject O(params (string name, JsonNode? value)[] properties)
    {
        var result = new JsonObject();
        foreach (var (name, value) in properties) result[name] = value;
        return result;
    }

    public static JsonNode Error(Exception e) => new JsonObject { ["$error"] = e.GetType().Name };

    public static JsonNode? Try(Func<JsonNode?> func)
    {
        try
        {
            return func();
        }
        catch (Exception e)
        {
            return Error(e);
        }
    }

    // Reading the input.

    public static string? Str(this JsonElement e, string name) => e.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null ? p.GetString() : null;

    public static int Int(this JsonElement e, string name) => e.GetProperty(name).GetInt32();

    public static int? IntN(this JsonElement e, string name) => e.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null ? p.GetInt32() : null;

    public static bool Bool(this JsonElement e, string name) => e.TryGetProperty(name, out var p) && p.GetBoolean();

    public static double Dbl(this JsonElement e) => e.ValueKind == JsonValueKind.String
        ? e.GetString() switch
        {
            "NaN" => double.NaN,
            "Infinity" => double.PositiveInfinity,
            "-Infinity" => double.NegativeInfinity,
            "-0" => -0.0,
            var x => throw new ArgumentException(x),
        }
        : e.GetDouble();

    public static IEnumerable<JsonElement> Arr(this JsonElement e, string name) => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Array ? p.EnumerateArray() : [];

    public static bool Has(this JsonElement e, string name) => e.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null;

    public static int IndexOfRef<T>(IEnumerable<T> list, T? item) where T : class
    {
        var i = 0;
        foreach (var x in list)
        {
            if (ReferenceEquals(x, item)) return i;
            i++;
        }

        return -1;
    }

    public static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
