namespace OneShelf.Common.OpenAi.Models;

public class MusicGenerationResult
{
    public required byte[] AudioData { get; init; } // Raw audio bytes
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}