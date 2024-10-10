namespace OneShelf.Common.OpenAi.Models.Memory;

public class UserImageMessageMemoryPoint(string base64) : MemoryPoint
{
    public string Base64 { get; } = base64;
}