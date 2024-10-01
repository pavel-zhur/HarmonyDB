using OneShelf.Common;

namespace OneShelf.Frontend.Web.Services;

public class ChordsCacheLoader : CacheLoaderBase
{
    private static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(20);

    private readonly CollectionIndexProvider _collectionIndexProvider;
    private readonly DataProvider _dataProvider;
    private readonly Api _api;

    public ChordsCacheLoader(ILogger<ChordsCacheLoader> logger, CollectionIndexProvider collectionIndexProvider, DataProvider dataProvider, Api api)
        : base(logger)
    {
        _collectionIndexProvider = collectionIndexProvider;
        _dataProvider = dataProvider;
        _api = api;
    }

    protected override async Task Work(CancellationToken token)
    {
        var collectionIndex = _collectionIndexProvider.Peek().collectionIndex;
        if (collectionIndex == null)
        {
            return;
        }

        var existing = await _dataProvider.GetChordsExternalIdsAvailableInCache();

        var total = collectionIndex.SongsById.Values
            .SelectMany(x => x.Versions.Select(x => x.ExternalId))
            .Where(x => x != null)
            .Cast<string>()
            .Distinct()
            .ToList();

        var externalIds = total
            .Where(x => !existing.Contains(x))
            .ToList();

        var groups = externalIds.Chunk(30).ToList();

        Task UpdateProgress() => OnUpdated(existing, existing.Count * 100 / total.Count);

        await UpdateProgress();

        var nextTask = groups.Any() ? _api.GetChords(groups[0], token) : null;

        for (var i = 0; i < groups.Count; i++)
        {
            if (token.IsCancellationRequested) break;

            if (nextTask == null) break;
            var got = await nextTask;
            nextTask = i + 1 < groups.Count ? _api.GetChords(groups[i + 1], token) : null;

            foreach (var ((externalId, chords), j) in got.WithIndices())
            {
                if (token.IsCancellationRequested) break;
                await _dataProvider.SaveChords(externalId, chords);
                await Task.Delay(20, token);
                existing.Add(externalId);
                await UpdateProgress();
            }

            await Task.Delay(300, token);
        }
    }

    public async void StartDelayed()
    {
        await Task.Delay(FirstDelay);
        Start();
    }
}