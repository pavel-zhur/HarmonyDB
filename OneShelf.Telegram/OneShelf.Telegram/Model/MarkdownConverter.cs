using System.Text.Json;
using System.Text.Json.Serialization;
using OneShelf.Telegram.Helpers;

namespace OneShelf.Telegram.Model;

internal class MarkdownConverter : JsonConverter<Markdown>
{
    public override Markdown? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetString()!.ToMarkdown();
    }

    public override void Write(Utf8JsonWriter writer, Markdown value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}