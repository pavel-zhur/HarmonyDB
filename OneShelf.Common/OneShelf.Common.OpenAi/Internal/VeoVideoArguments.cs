using System.Text.Json.Serialization;

namespace OneShelf.Common.OpenAi.Internal;

internal class VeoVideoArguments
{
    [JsonPropertyName("prompt")]
    public string? Prompt { get; set; }

    [JsonPropertyName("negative_prompt")]
    public string? NegativePrompt { get; set; }
}