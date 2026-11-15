using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyDB.Common.Representations.OneShelf;
using static DotnetReference.J;

namespace DotnetReference;

public static partial class Suites
{
    /// <summary>The options of the places the original serializes: the API clients (ApiClientBase) and JsonSerializer's defaults
    /// (what the site stores on the device).</summary>
    public static readonly JsonSerializerOptions Camel = CreateCamel();

    private static JsonSerializerOptions CreateCamel()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    public static readonly JsonSerializerOptions Pascal = JsonSerializerOptions.Default;

    /// <summary>The model types the TypeScript ports have JSON contracts for, by their full names.</summary>
    public static readonly IReadOnlyDictionary<string, Type> ModelTypes = new[]
        {
            typeof(NodeHtml).Assembly,
            typeof(OneShelf.Frontend.Api.Model.V3.Databasish.Collection).Assembly,
            typeof(HarmonyDB.Source.Api.Model.V1.Chords).Assembly,
            typeof(OneShelf.Collectives.Api.Model.V2.Sub.Collective).Assembly,
            typeof(OneShelf.Authorization.Api.Model.Identity).Assembly,
        }
        .SelectMany(a => a.GetTypes())
        .Where(t => t.FullName != null)
        .GroupBy(t => t.FullName!)
        .ToDictionary(g => g.Key, g => g.Single());

    private static JsonNode Json(JsonElement input)
    {
        var type = ModelTypes[input.Str("type")!];
        var json = input.Str("json")!;
        var options = input.Str("naming") == "camel" ? Camel : Pascal;
        var value = JsonSerializer.Deserialize(json, type, options);
        return A(S(JsonSerializer.Serialize(value, type, Camel)), S(JsonSerializer.Serialize(value, type, Pascal)));
    }
}
