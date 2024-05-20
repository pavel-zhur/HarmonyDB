using System.Text.Json.Serialization;

namespace OneShelf.Common.OpenAi.Internal;

internal class ImageArguments
{
    [JsonPropertyName("image_prompts")]
    public List<string?>? ImagePrompts { get; set; }
}