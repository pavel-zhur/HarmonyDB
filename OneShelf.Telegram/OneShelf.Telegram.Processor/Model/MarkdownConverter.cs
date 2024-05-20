using Newtonsoft.Json;

namespace OneShelf.Telegram.Processor.Model;

public class MarkdownConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var markdown = (Markdown)value!;
        writer.WriteValue(markdown?.ToString());
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var stringValue = reader.ReadAsString();
        return stringValue == null ? null : Markdown.UnsafeFromMarkdownString(stringValue);
    }

    public override bool CanConvert(Type objectType) => objectType == typeof(Markdown);
}