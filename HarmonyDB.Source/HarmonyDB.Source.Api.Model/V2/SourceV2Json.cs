using System.Text.Json;
using System.Text.Json.Serialization;

namespace HarmonyDB.Source.Api.Model.V2;

/// <summary>
/// The wire format of the source contract, version 2: camel-case names, enums as camel-case strings
/// (<c>"noChord"</c>, <c>"youtube"</c>). Every server and client of the contract, and every store of its models, reads
/// and writes with these options and no others.
/// </summary>
public static class SourceV2Json
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
