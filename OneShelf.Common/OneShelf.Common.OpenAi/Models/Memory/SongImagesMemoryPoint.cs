using System.Text.Json.Nodes;

namespace OneShelf.Common.OpenAi.Models.Memory;

public class SongImagesMemoryPoint
{
    /// <summary>
    /// The request to the model and its answers, as they went.
    /// </summary>
    public required JsonNode Trace { get; init; }

    public required List<List<string>> ImageTraces { get; init; }
}
