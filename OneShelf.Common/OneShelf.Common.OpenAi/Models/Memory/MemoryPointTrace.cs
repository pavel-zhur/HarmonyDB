using OpenAI.Chat;

namespace OneShelf.Common.OpenAi.Models.Memory;

public record MemoryPointTrace(ChatRequest Request, ChatResponse Response);