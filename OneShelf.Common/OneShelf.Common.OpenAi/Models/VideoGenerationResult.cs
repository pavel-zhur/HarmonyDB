namespace OneShelf.Common.OpenAi.Models;

public class VideoGenerationResult
{
    public required byte[] VideoData { get; init; } // Raw video bytes
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}