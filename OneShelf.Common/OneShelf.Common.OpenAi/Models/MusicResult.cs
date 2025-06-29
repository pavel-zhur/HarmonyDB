namespace OneShelf.Common.OpenAi.Models;

public record MusicResult(byte[] Data, string Prompt, string? Title);