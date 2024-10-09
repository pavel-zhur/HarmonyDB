namespace OneShelf.Common.OpenAi.Models.Memory;

public class ChatBotMemoryPointWithTraces : ChatBotMemoryPointWithDeserializableTraces
{
    public List<MemoryPointTrace> Traces { get; init; } = new();
}