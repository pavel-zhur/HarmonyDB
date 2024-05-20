namespace OneShelf.Common.OpenAi.Models.Memory;

public class ChatBotMemoryPointWithTraces : ChatBotMemoryPoint
{
    public List<MemoryPointTrace> Traces { get; init; } = new();

    public List<string> ImageTraces { get; init; } = new();

    public List<string> ImageUrlTraces { get; init; } = new();
}