using OpenAI.Chat;

namespace OneShelf.Common.OpenAi.Models.Memory;

public class ChatBotMemoryPoint : MemoryPoint
{
    public List<Message> Messages { get; init; } = new();

    public bool IsTopicChangeDetected { get; set; }
}