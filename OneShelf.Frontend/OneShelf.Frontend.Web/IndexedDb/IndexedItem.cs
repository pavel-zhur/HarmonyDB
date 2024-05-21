using DnetIndexedDb;

namespace OneShelf.Frontend.Web.IndexedDb;

public class IndexedItem
{
    [IndexDbKey]
    public required string Key { get; init; }

    public required int Version { get; init; }

    public required string Contents { get; init; }
}