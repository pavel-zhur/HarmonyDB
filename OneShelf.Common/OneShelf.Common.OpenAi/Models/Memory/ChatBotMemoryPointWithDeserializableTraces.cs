namespace OneShelf.Common.OpenAi.Models.Memory;

public class ChatBotMemoryPointWithDeserializableTraces : ChatBotMemoryPoint
{
    public List<string> ImageTraces { get; init; } = new();

    public List<string> ImageUrlTraces { get; init; } = new();
}