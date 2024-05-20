namespace OneShelf.OneDog.Processor.Model.IoMemories;

public record IoMemoryItemOptions : IoMemoryItem
{
    public IoMemoryItemOptions(IReadOnlyCollection<string>? options)
    {
        Options = options == null ? null : string.Join(";-;", options);
    }

    public string? Options { get; }
}