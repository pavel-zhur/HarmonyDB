using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using static DotnetReference.J;

namespace DotnetReference;

// System.Uri as the site uses it (src/lib/common/dotnet/Uri.ts), and the string hash (Marvin, hashCode.ts).
// A string that is not valid UTF-16 (a lone surrogate) travels as the array of its code units, both ways.
public static partial class Suites
{
    private delegate int MarvinHash(ref byte data, uint count, uint p0, uint p1);

    private delegate int MarvinHashIgnoreCase(ref char data, int count, uint p0, uint p1);

    private static readonly Type MarvinType = typeof(string).Assembly.GetType("System.Marvin", true)!;

    private static readonly MarvinHash Marvin = MarvinType
        .GetMethod("ComputeHash32", BindingFlags.Public | BindingFlags.Static, [typeof(byte).MakeByRefType(), typeof(uint), typeof(uint), typeof(uint)])!
        .CreateDelegate<MarvinHash>();

    private static readonly MarvinHashIgnoreCase MarvinIgnoreCase = MarvinType
        .GetMethod("ComputeHash32OrdinalIgnoreCase", BindingFlags.Public | BindingFlags.Static, [typeof(char).MakeByRefType(), typeof(int), typeof(uint), typeof(uint)])!
        .CreateDelegate<MarvinHashIgnoreCase>();

    private static readonly ulong MarvinDefaultSeed = (ulong)MarvinType.GetProperty("DefaultSeed", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

    /// <summary>The fixed seed of src/lib/common/dotnet/hashCode.ts (marvinSeed).</summary>
    private const uint SeedP0 = 0x9e3779b9, SeedP1 = 0x7f4a7c15;

    private static int MarvinOf(string s, uint p0, uint p1)
    {
        var bytes = MemoryMarshal.AsBytes(s.AsSpan()).ToArray();
        return Marvin(ref MemoryMarshal.GetArrayDataReference(bytes), (uint)bytes.Length, p0, p1);
    }

    private static int MarvinIgnoreCaseOf(string s, uint p0, uint p1)
    {
        var chars = s.ToCharArray();
        return MarvinIgnoreCase(ref MemoryMarshal.GetArrayDataReference(chars), chars.Length, p0, p1);
    }

    private static bool IsValidUtf16(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) i++;
            else if (char.IsSurrogate(s[i])) return false;
        }

        return true;
    }

    /// <summary>A string out: as it is, or the array of its code units when it is not valid UTF-16.</summary>
    private static JsonNode? T(string? s) => s == null ? null : IsValidUtf16(s) ? S(s) : A(s.Select(c => (JsonNode?)I(c)));

    /// <summary>A string in: a string, or the array of its code units.</summary>
    private static string Text(JsonElement e) => e.ValueKind == JsonValueKind.Array ? new string(e.EnumerateArray().Select(x => (char)x.GetInt32()).ToArray()) : e.GetString()!;

    private static JsonNode UriSuite(JsonElement input)
    {
        switch (input.Str("f"))
        {
            case "uri":
            {
                var strings = input.Arr("u").Select(Text).ToList();
                var parsed = new List<Uri?>();
                var result = new JsonArray();
                foreach (var s in strings)
                {
                    var any = Uri.TryCreate(s, UriKind.RelativeOrAbsolute, out var u) ? u : null;
                    parsed.Add(any);
                    result.Add(A(
                        any == null ? null : DescribeUri(any),
                        Uri.TryCreate(s, UriKind.Absolute, out var absolute) ? DescribeUri(absolute) : null,
                        Uri.TryCreate(s, UriKind.Relative, out var relative) ? DescribeUri(relative) : null,
                        Try(() => DescribeUri(new Uri(s))),
                        Try(() => DescribeUri(new Uri(s, UriKind.RelativeOrAbsolute))),
                        Try(() =>
                        {
                            var read = JsonSerializer.Deserialize<Uri>(JsonSerializer.Serialize(s));
                            return A(T(read!.OriginalString), T(JsonSerializer.Serialize(read)), DescribeUri(read));
                        }),
                        T(Uri.EscapeDataString(s))));
                }

                // == between every two of them (the ones TryCreate made), in order.
                var equality = new JsonArray();
                for (var i = 0; i < parsed.Count; i++)
                {
                    for (var j = 0; j < parsed.Count; j++)
                    {
                        if (parsed[i] != null && parsed[j] != null) equality.Add(B(parsed[i] == parsed[j]));
                    }
                }

                return O(("each", result), ("eq", equality));
            }
            case "combine":
            {
                var baseText = Text(input.GetProperty("b"));
                return A(input.Arr("r").Select(r => Try(() => DescribeUri(new Uri(new Uri(baseText, UriKind.RelativeOrAbsolute), r.ValueKind == JsonValueKind.Null ? null : Text(r))))));
            }
            case "json":
                return A(input.Strings("raw").Select(raw => Try(() => T(JsonSerializer.Serialize(JsonSerializer.Deserialize<Uri>(raw))))));
            case "marvin":
                return A(input.Arr("s").Select(Text).Select(s => (JsonNode?)A(
                    I(MarvinOf(s, SeedP0, SeedP1)),
                    I(MarvinIgnoreCaseOf(s, SeedP0, SeedP1)),
                    // string.GetHashCode is Marvin with the process's random seed: checked here, not output.
                    B(s.GetHashCode() == MarvinOf(s, (uint)MarvinDefaultSeed, (uint)(MarvinDefaultSeed >> 32))),
                    B(StringComparer.OrdinalIgnoreCase.GetHashCode(s) == MarvinIgnoreCaseOf(s, (uint)MarvinDefaultSeed, (uint)(MarvinDefaultSeed >> 32))))));
            default:
                throw new ArgumentOutOfRangeException("f");
        }
    }

    /// <summary>What the site reads of a Uri, in this order: ToString, Host, the equality key and GetHashCode against it,
    /// and the Uri made again from ToString (as a visit is sent).</summary>
    private static JsonNode DescribeUri(Uri u)
    {
        var text = u.ToString();
        JsonNode? key = null, hash = null;
        if (u.IsAbsoluteUri)
        {
            var components = UriComponents.HttpRequestUrl | (u.Scheme == Uri.UriSchemeMailto ? UriComponents.UserInfo : 0);
            var keyText = u.GetComponents(components, UriFormat.SafeUnescaped);
            key = T(keyText);
            hash = A(B(u.GetHashCode() == keyText.GetHashCode()), B(u.GetHashCode() == StringComparer.OrdinalIgnoreCase.GetHashCode(keyText)));
        }
        else
        {
            hash = A(B(u.GetHashCode() == u.OriginalString.GetHashCode()), B(false));
        }

        return A(
            B(u.IsAbsoluteUri),
            T(u.OriginalString),
            T(text),
            Try(() => T(u.Host)),
            Try(() => I(u.Port)),
            key,
            hash,
            Try(() => T(u.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped))),
            Try(() => T(new Uri(text).ToString())));
    }
}
