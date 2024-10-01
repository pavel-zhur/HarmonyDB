using OneShelf.Frontend.Web.IndexedDb;
using SpawnDev.BlazorJS.JSObjects;

namespace OneShelf.Frontend.Web.Services;

public class MyIndexedDb(ILogger<MyIndexedDb> logger) : IDisposable
{
    private const int DataVersion = 4;

    private IDBFactory? _idbFactory;
    private IDBDatabase? _idb;

    public async Task<IndexedItem?> GetItem(IndexedItemType type, string key)
    {
        await Initialize();
        var storeName = GetStoreName(type);
        using var transaction = _idb!.Transaction(storeName, "readonly");
        using var store = transaction.ObjectStore<string, IndexedItem>(storeName);
        return await store.GetAsync(key);
    }

    public async Task<List<IndexedItem>> GetItems(IndexedItemType type)
    {
        await Initialize();
        var storeName = GetStoreName(type);
        using var transaction = _idb!.Transaction(storeName, "readonly");
        using var store = transaction.ObjectStore<string, IndexedItem>(storeName);
        return (await store.GetAllAsync()).ToList();
    }

    public async Task<HashSet<string>> GetKeys(IndexedItemType type)
    {
        await Initialize();
        var storeName = GetStoreName(type);
        using var transaction = _idb!.Transaction(storeName, "readonly");
        using var store = transaction.ObjectStore<string, IndexedItem>(storeName);
        return (await store.GetAllKeysAsync()).ToList().ToHashSet();
    }

    public async Task Save(IndexedItemType type, string key, string contents)
    {
        await Initialize();
        var storeName = GetStoreName(type);
        using var transaction = _idb!.Transaction(storeName, true);
        using var store = transaction.ObjectStore<string, IndexedItem>(storeName);
        await store.AddAsync(new()
        {
            Contents = contents,
            Key = key,
        });
    }

    public async Task DeleteAll(params IndexedItemType[] types)
    {
        await Initialize();
        var names = types.Select(GetStoreName).ToList();
        using var transaction = _idb!.Transaction(new(names), true);
        foreach (var type in names)
        {
            using var store = transaction.ObjectStore<string, IndexedItem>(type);
            store.Clear();
        }
    }

    private async Task Initialize()
    {
        var dbName = "OneShelfDatabase";
        _idbFactory = new();
        _idb = await _idbFactory.OpenAsync(dbName, 11, (evt) =>
        {
            using var request = evt.Target;
            using var db = request.Result;
            var stores = db.ObjectStoreNames;

            foreach (var name in stores.Except(Enum.GetValues<IndexedItemType>().Select(GetStoreName)))
            {
                if (stores.Contains(name))
                {
                    db.DeleteObjectStore(name);
                    logger.LogInformation($"Dropped object store {name}");
                }
            }

            foreach (var indexedItemType in Enum.GetValues<IndexedItemType>())
            {
                using var _ = db.CreateObjectStore<string, IndexedItem>(GetStoreName(indexedItemType), new()
                {
                    KeyPath = nameof(IndexedItem.Key).ToLowerInvariant(),
                });
            }
        });
    }

    private static string GetStoreName(IndexedItemType indexedItemType)
    {
        return $"{nameof(IndexedItem)}-{indexedItemType.ToString()}-{DataVersion}";
    }

    public void Dispose()
    {
        _idb?.Dispose();
        _idbFactory?.Dispose();
    }
}