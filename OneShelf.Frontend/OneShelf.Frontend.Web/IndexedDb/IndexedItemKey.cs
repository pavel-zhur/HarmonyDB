using DnetIndexedDb;

namespace OneShelf.Frontend.Web.IndexedDb;

public class IndexedItemKey
{
    [IndexDbKey]
    public required string Key { get; init; }

    public required int Version { get; init; }
}