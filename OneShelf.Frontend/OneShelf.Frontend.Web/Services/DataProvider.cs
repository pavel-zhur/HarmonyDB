using System.Collections.Concurrent;
using System.Text.Json;
using HarmonyDB.Index.Analysis.Models.V1;
using HarmonyDB.Sources.Api.Model.V1;
using Nito.AsyncEx;
using OneShelf.Common;
using OneShelf.Frontend.SpecificModel;
using OneShelf.Frontend.Web.IndexedDb;

namespace OneShelf.Frontend.Web.Services;

public class DataProvider
{
    private const string ChordsKeyPrefix = "Chords-";
    private const string ChordsProgressionKeyPrefix = "ChordsProgression-";
    private const string SearchKeyPrefix = "Search-";
    private readonly Api _api;
    private readonly MyIndexedDb _myIndexedDb;
    private readonly ILogger<DataProvider> _logger;
    private readonly AsyncLock _savingLock = new();

    private readonly ConcurrentDictionary<string, object> _deserialized = new();

    public DataProvider(Api api, ILogger<DataProvider> logger, MyIndexedDb myIndexedDb)
    {
        _api = api;
        _logger = logger;

        _myIndexedDb = myIndexedDb;
    }

    public async Task<Chords> GetChords(string externalId, bool reloadUnstable = true)
        => await Cache(
            ChordsKeyPrefix,
            $"{ChordsKeyPrefix}{externalId}", 
            async () => (await _api.GetChords(externalId.Once().ToList())).Single().Value, 2,
            c => !c.IsStable && reloadUnstable);

    public async Task ClearProgressions()
    {
        await Task.Yield();
        await _myIndexedDb.OpenIndexedDb();
        var keysRange = await GetKeysRange(ChordsProgressionKeyPrefix);

        foreach (var key in keysRange)
        {
            using var _ = await _savingLock.LockAsync();
            try
            {
                await _myIndexedDb.DeleteByKey<string, IndexedItemKey>(key.key.Key);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            try
            {
                await _myIndexedDb.DeleteByKey<string, IndexedItem>(key.key.Key);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    public async Task<List<(IndexedItem item, string id)>> GetProgressions()
    {
        await _myIndexedDb.OpenIndexedDb();
        var progressions = await GetItemsRange(ChordsProgressionKeyPrefix);
        return progressions;
    }

    public async Task SaveChordsProgression(string externalId, List<ChordProgressionDataV1> progression)
    {
        using var _ = await _savingLock.LockAsync();

        var key = $"{ChordsProgressionKeyPrefix}{externalId}";

        await _myIndexedDb.AddItems(
            new IndexedItem
            {
                Key = key,
                Contents = JsonSerializer.Serialize(CompressedChordProgressionDataV1.Compress(progression)),
                Version = 1,
            }.Once().ToList());
        await _myIndexedDb.AddItems(
            new IndexedItemKey
                {
                    Key = key,
                    Version = 1,
                }
                .Once().ToList());
    }

    public async Task SaveChords(string externalId, Chords chords)
    {
        string GetKey(string externalId) => $"{ChordsKeyPrefix}{externalId}";

        _deserialized[GetKey(externalId)] = chords;

        await _myIndexedDb.OpenIndexedDb();

        try
        {
            using var _ = await _savingLock.LockAsync();
            await _myIndexedDb.AddItems(
                new IndexedItem
                {
                    Key = GetKey(externalId),
                    Contents = JsonSerializer.Serialize(chords),
                    Version = 1,
                }.Once().ToList());
            await _myIndexedDb.AddItems(
                new IndexedItemKey
                    {
                        Key = GetKey(externalId),
                        Version = 1,
                    }
                    .Once().ToList());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "These chords might have already been added.");
        }
    }

    public async Task<List<SearchHeader>> Search(string query)
        => await Cache(SearchKeyPrefix, $"{SearchKeyPrefix}{query}", () => _api.Search(query), 2);

    public List<SearchHeader>? SearchPeek(string query)
        => _deserialized.TryGetValue($"{SearchKeyPrefix}{query}", out var result) ? (List<SearchHeader>)result : null;

    private async Task<TItem> Cache<TItem>(string prefix, string key, Func<Task<TItem>> getter, int version, Func<TItem, bool>? forceReload = null)
    {
        if (_deserialized.TryGetValue(key, out var value))
        {
            return (TItem)value;
        }

        TItem? item = default;

        if (item == null)
        {
            await Task.Yield();
            await _myIndexedDb.OpenIndexedDb();
            var indexedItem = await _myIndexedDb.GetByKey<string, IndexedItem>(key);
            if (indexedItem != null)
            {
                item = JsonSerializer.Deserialize<TItem>(indexedItem.Contents);
            }

            if (item != null && (forceReload != null && forceReload(item) || (indexedItem?.Version ?? version) != version))
            {
                if (indexedItem != null && indexedItem.Version != version)
                {
                    await Clear(prefix, version);
                }

                item = default;
            }
        }

        if (item == null)
        {
            item = await getter();

            await Task.Yield();
            using var _ = await _savingLock.LockAsync();
            await _myIndexedDb.AddItems(new IndexedItem
            {
                Key = key,
                Contents = JsonSerializer.Serialize(item),
                Version = version,
            }.Once().ToList());
            await _myIndexedDb.AddItems(new IndexedItemKey
            {
                Key = key,
                Version = version,
            }.Once().ToList());
        }

        _deserialized[key] = item!;

        return item;
    }

    private async Task Clear(string prefix, int version)
    {
        var keys = await GetKeysRange(prefix);
        foreach (var key in keys)
        {
            if (key.key.Version != version)
            {
                try
                {
                    await _myIndexedDb.DeleteByKey<string, IndexedItem>(key.key.Key);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error deleting the key.");
                }

                try
                {
                    await _myIndexedDb.DeleteByKey<string, IndexedItemKey>(key.key.Key);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error deleting the item.");
                }
            }
        }
    }

    public async Task Clear()
    {
        _deserialized.Clear();

        await _myIndexedDb.OpenIndexedDb();
        await _myIndexedDb.DeleteAll<IndexedItem>();
        await _myIndexedDb.DeleteAll<IndexedItemKey>();
    }

    public async Task<HashSet<string>> GetChordsExternalIdsAvailableInCache()
    {
        await Task.Yield();
        await _myIndexedDb.OpenIndexedDb();
        return (await GetKeysRange(ChordsKeyPrefix))
            .Select(x => x.id)
            .ToHashSet();
    }

    public async Task<HashSet<string>> GetProgressionsExternalIdsAvailableInCache()
    {
        await Task.Yield();
        await _myIndexedDb.OpenIndexedDb();
        return (await GetKeysRange(ChordsProgressionKeyPrefix))
            .Select(x => x.id)
            .ToHashSet();
    }

    private async Task<List<(IndexedItemKey key, string id)>> GetKeysRange(string prefix)
    {
        return (await _myIndexedDb.GetRange<string, IndexedItemKey>(prefix, $"{prefix}x")).Select(x => (x, x.Key.Substring(prefix.Length))).ToList();
    }

    private async Task<List<(IndexedItem item, string id)>> GetItemsRange(string prefix)
    {
        return (await _myIndexedDb.GetRange<string, IndexedItem>(prefix, $"{prefix}x")).Select(x => (x, x.Key.Substring(prefix.Length))).ToList();
    }
}