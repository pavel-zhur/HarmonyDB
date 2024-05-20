namespace OneShelf.Common.OpenAi.Models.Memory;

public class SongImagesMemoryPoint
{
    public required MemoryPointTrace Trace { get; init; }

    public required List<List<string>> ImageTraces { get; init; }
}