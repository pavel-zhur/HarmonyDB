using OneShelf.Common;
using System.Text.Json;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.V1;
using HarmonyDB.Index.Analysis.Services;
using Nito.AsyncEx;
using OneShelf.Common.Songs.Hashing;
using OneShelf.Frontend.Api.Model.V3.Databasish;
using OneShelf.Frontend.Api.Model.V3.Databasish.Interfaces;
using Version = OneShelf.Frontend.Api.Model.V3.Databasish.Version;

namespace OneShelf.Frontend.Web.Services;

public class ProgressionsCacheLoader : CacheLoaderBase
{
    private readonly DataProvider _dataProvider;
    private readonly CollectionIndexProvider _collectionIndexProvider;
    private readonly ProgressionsBuilder _progressionsBuilder;
    private readonly ChordDataParser _chordDataParser;
    private readonly AsyncLock _lock = new();
    
    private (List<(ISong song, Version version, ChordsProgression progression)>? result, int hash)? _cache;

    public ProgressionsCacheLoader(ILogger<ProgressionsCacheLoader> logger, DataProvider dataProvider, CollectionIndexProvider collectionIndexProvider, ProgressionsBuilder progressionsBuilder, ChordDataParser chordDataParser)
        : base(logger)
    {
        _dataProvider = dataProvider;
        _collectionIndexProvider = collectionIndexProvider;
        _progressionsBuilder = progressionsBuilder;
        _chordDataParser = chordDataParser;
    }

    protected override async Task Work(CancellationToken token)
    {
        await Get(false, token);
    }

    public async Task<List<(ISong song, Version version, ChordsProgression progression)>?> Get()
        => await Get(true, default);

    public List<(ISong song, Version version, ChordsProgression progression)>? Peek() => _cache?.result;

    private async Task<List<(ISong song, Version version, ChordsProgression progression)>?> Get(bool onlyFast, CancellationToken cancellationToken)
    {
        if (_collectionIndexProvider.Peek().collectionIndex is not { } collectionIndex) return null;

        using var _ = await _lock.LockAsync();

        var externalIds = collectionIndex.SongsById
            .SelectMany(x => x.Value.Versions)
            .Where(x => x.ExternalId != null)
            .Select(x => x.ExternalId!)
            .ToList();

        var hash = MyHashExtensions.CalculateHash(externalIds.OrderBy(x => x).Select(x => x.GetHashCode()));

        if (_cache?.hash == hash) return _cache.Value.result;
        var dbProgressions = await _dataProvider.GetProgressions();

        cancellationToken.ThrowIfCancellationRequested();

        var progressionsProgress = dbProgressions.Select(x => x.Key).ToHashSet();

        var availableChords = await _dataProvider.GetChordsExternalIdsAvailableInCache();

        cancellationToken.ThrowIfCancellationRequested();

        var toGet = availableChords.Intersect(externalIds).Except(dbProgressions.Select(x => x.Key)).ToList();

        if ((toGet.Count > CacheLoaderBase.MissingProgressionsThreshold || (externalIds.Count - availableChords.Count) > CacheLoaderBase.MissingChordsThresholdForProgressionsOnlyFastFail) && onlyFast) return null;

        var progressions = new Dictionary<string, ChordsProgression>();
        var chordsFactor = 50; // reading chords will take 50 times longer
        var total = dbProgressions.Count + toGet.Count * chordsFactor;
        var progress = 0;
        foreach (var (item, i) in dbProgressions.WithIndices())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (progress != (progress = i * 100 / total / 10 * 10))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
                await OnUpdated(progressionsProgress, progress);
            }

            progressions[item.Key] = _progressionsBuilder.BuildProgression(JsonSerializer.Deserialize<CompressedChordsProgressionDataV1>(item.Contents)!.Decompress());
        }

        foreach (var (externalId, i) in toGet.WithIndices())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (progress != (progress = (dbProgressions.Count + i * chordsFactor) * 100 / total))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
                await OnUpdated(progressionsProgress, progress);
            }

            try
            {
                var chords = await _dataProvider.GetChords(externalId, false);
                var progression = chords.Output.AsChords(new()).Select(_chordDataParser.GetProgressionData).ToList();

                await _dataProvider.SaveChordsProgression(externalId, progression);
                progressionsProgress.Add(externalId);

                progressions[externalId] = _progressionsBuilder.BuildProgression(progression);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error getting the chords progression for {externalId}.", externalId);
            }
        }

        var result = collectionIndex.VersionsById.Values
            .Where(x => x.version.ExternalId != null)
            .Select(x => (x, progression: progressions.GetValueOrDefault(x.version.ExternalId!)))
            .Where(x => x.progression != null)
            .Select(x => (x.x.song, x.x.version, x.progression!))
            .OrderBy(x => x.song.Index)
            .ToList();

        _cache = (result, hash);
        await OnFinished();
        return result;
    }
}