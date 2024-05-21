namespace OneShelf.Common.OpenAi.Models.Memory;

public class UserMessageMemoryPoint : MemoryPoint
{
    public UserMessageMemoryPoint(string message) => Message = message;
    public string Message { get; init; }
}