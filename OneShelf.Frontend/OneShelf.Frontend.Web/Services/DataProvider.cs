using System.Collections.Concurrent;
using System.Text.Json;
using HarmonyDB.Index.Analysis.Models.V1;
using HarmonyDB.Source.Api.Model.V1;
using Nito.AsyncEx;
using OneShelf.Common;
using OneShelf.Frontend.Web.IndexedDb;

namespace OneShelf.Frontend.Web.Services;

public class DataProvider
{
    private readonly Api _api;
    private readonly ILogger<DataProvider> _logger;
    private readonly AsyncLock _savingLock = new();
    private readonly MyIndexedDb _myIndexedDb;

    private readonly ConcurrentDictionary<(IndexedItemType type, string key), object> _deserialized = new();

    public DataProvider(Api api, ILogger<DataProvider> logger, MyIndexedDb myIndexedDb)
    {
        _api = api;
        _logger = logger;
        _myIndexedDb = myIndexedDb;
    }

    public async Task<Chords> GetChords(string externalId, bool reloadUnstable = true)
        => await Cache(
            IndexedItemType.Chords,
            externalId,
            async () => (await _api.GetChords(externalId.Once().ToList())).Single().Value, 
            c => !c.IsStable && reloadUnstable);

    public async Task ClearProgressions()
    {
        await Task.Yield();
        await _myIndexedDb.DeleteAll(IndexedItemType.Progression);
    }

    public async Task<List<IndexedItem>> GetProgressions()
    {
        return await _myIndexedDb.GetItems(IndexedItemType.Progression);
    }

    public async Task SaveChordsProgression(string externalId, List<ChordDataV1> progression)
    {
        using var _ = await _savingLock.LockAsync();
        await _myIndexedDb.Save(IndexedItemType.Progression, externalId, JsonSerializer.Serialize(CompressedChordsProgressionDataV1.Compress(progression)));
    }

    public async Task SaveChords(string externalId, Chords chords)
    {
        _deserialized[(IndexedItemType.Chords, externalId)] = chords;
        
        try
        {
            using var _ = await _savingLock.LockAsync();
            await _myIndexedDb.Save(IndexedItemType.Chords, externalId, JsonSerializer.Serialize(chords));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "These chords might have already been added.");
        }
    }

    public async Task<List<SearchHeader>> Search(string query)
        => await Cache(IndexedItemType.Search, query, () => _api.Search(query));

    public List<SearchHeader>? SearchPeek(string query)
        => _deserialized.TryGetValue((IndexedItemType.Search, query), out var result) ? (List<SearchHeader>)result : null;

    private async Task<TItem> Cache<TItem>(IndexedItemType type, string key, Func<Task<TItem>> getter, Func<TItem, bool>? forceReload = null)
    {
        if (_deserialized.TryGetValue((type, key), out var value))
        {
            return (TItem)value;
        }

        TItem? item = default;

        if (item == null)
        {
            await Task.Yield();
            var indexedItem = await _myIndexedDb.GetItem(type, key);
            if (indexedItem != null)
            {
                item = JsonSerializer.Deserialize<TItem>(indexedItem.Contents);
            }

            else if (item != null && forceReload != null && forceReload(item))
            {
                item = default;
            }
        }

        if (item == null)
        {
            item = await getter();

            await Task.Yield();
            using var _ = await _savingLock.LockAsync();
            await _myIndexedDb.Save(type, key, JsonSerializer.Serialize(item));
        }

        _deserialized[(type, key)] = item!;

        return item;
    }

    public async Task Clear()
    {
        _deserialized.Clear();

        await _myIndexedDb.DeleteAll(IndexedItemType.Chords, IndexedItemType.Progression, IndexedItemType.Search);
    }

    public async Task<HashSet<string>> GetChordsExternalIdsAvailableInCache()
    {
        await Task.Yield();
        return await _myIndexedDb.GetKeys(IndexedItemType.Chords);
    }

    public async Task<HashSet<string>> GetProgressionsExternalIdsAvailableInCache()
    {
        await Task.Yield();
        return await _myIndexedDb.GetKeys(IndexedItemType.Progression);
    }
}