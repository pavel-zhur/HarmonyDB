using System.Text.Json.Serialization;

namespace OneShelf.Common.OpenAi.Internal;

internal class SoraVideoArguments
{
    [JsonPropertyName("prompt")]
    public string? Prompt { get; set; }

    [JsonPropertyName("ratio")]
    public string? Ratio { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; }
}