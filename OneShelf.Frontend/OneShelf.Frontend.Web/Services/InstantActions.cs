using Blazored.LocalStorage;
using Nito.AsyncEx;
using OneShelf.Common;
using OneShelf.Frontend.Api.Model.V3.Api;
using OneShelf.Frontend.Api.Model.V3.Instant;

namespace OneShelf.Frontend.Web.Services;

public class InstantActions
{
    private const string Key = "InstantActionsV1";

    private readonly ILocalStorageService _localStorageService;
    private readonly ILogger<InstantActions> _logger;
    private readonly AsyncLock _lock = new();

    public InstantActions(ILocalStorageService localStorageService, ILogger<InstantActions> logger)
    {
        _localStorageService = localStorageService;
        _logger = logger;
    }

    public event Action? CollectionRelevantUpdate;
    public event Action? LazyUpdate;

    public async Task VisitedChords(Uri? uri, string? externalId, string? searchQuery, int? transpose, int? songId,
        string artists, string title, string? source)
    {
        var modified = await Store(r =>
        {
            var newVisit = new VisitedChords
            {
                Uri = uri,
                ExternalId = externalId,
                SearchQuery = searchQuery,
                Transpose = transpose,
                HappenedOn = DateTime.Now,
                SongId = songId,
                Artists = artists,
                Title = title,
                Source = source,
            };

            if (r.VisitedChords.LastOrDefault()?.GetHashCode() != newVisit.GetHashCode())
            {
                r.VisitedChords.Add(newVisit);
                return true;
            }

            return false;
        });

        if (modified)
            OnLazyUpdate();
    }

    public async Task VisitedSearch(string query)
    {
        if (int.TryParse(query, out _)) return;

        var modified = await Store(r =>
        {
            var newVisit = new VisitedSearch
            {
                Query = query,
                HappenedOn = DateTime.Now,
            };

            if (r.VisitedSearches.LastOrDefault()?.GetHashCode() != newVisit.GetHashCode())
            {
                r.VisitedSearches.Add(newVisit);
                return true;
            }

            return false;
        });

        if (modified)
            OnLazyUpdate();
    }

    public async Task UpdateLike(int songId, int? versionId, byte? level, int? transpose, int? likeCategoryId)
    {
        await Store(r => r.UpdatedLikes.Add(new()
        {
            SongId = songId,
            VersionId = versionId,
            HappenedOn = DateTime.Now,
            Level = level,
            Transpose = transpose,
            LikeCategoryId = likeCategoryId,
        }));

        OnCollectionRelevantUpdate();
    }

    public async Task VersionRemove(int versionId)
    {
        await Store(r => r.RemovedVersions.Add(new()
        {
            HappenedOn = DateTime.Now,
            VersionId = versionId,
        }));

        OnCollectionRelevantUpdate();
    }

    public async Task VersionImport(string externalId, byte level, int transpose, int? likeCategoryId = null)
    {
        await Store(r => r.ImportedVersions.Add(new()
        {
            HappenedOn = DateTime.Now,
            Level = level,
            Transpose = transpose,
            ExternalId = externalId,
            LikeCategoryId = likeCategoryId,
        }));

        OnCollectionRelevantUpdate();
    }

    public async Task ExtractSent(ApplyRequest actions)
    {
        await Store(storage =>
        {
            var ids = actions.GetIds().ToHashSet();

            storage.ImportedVersions.RemoveAll(x => ids.Contains(x.ActionId));
            storage.RemovedVersions.RemoveAll(x => ids.Contains(x.ActionId));
            storage.UpdatedLikes.RemoveAll(x => ids.Contains(x.ActionId));
            storage.VisitedSearches.RemoveAll(x => ids.Contains(x.ActionId));
            storage.VisitedChords.RemoveAll(x => ids.Contains(x.ActionId));
        });
    }

    public async Task<ApplyRequest> Peek()
    {
        return (await _localStorageService.GetItemAsync<ApplyRequest>(Key)) ?? new ApplyRequest
        {
            Identity = null!,
        };
    }

    private async Task Store(Action<ApplyRequest> modifier)
    {
        await Store(r =>
        {
            modifier(r);
            return true;
        });
    }

    private async Task<bool> Store(Func<ApplyRequest, bool> modifier)
    {
        try
        {
            using var asyncLock = await _lock.LockAsync();
            var request = await _localStorageService.GetItemAsync<ApplyRequest?>(Key) ?? new ApplyRequest
            {
                Identity = null!,
            };

            var modified = modifier(request);
            if (!modified) return false;

            await _localStorageService.SetItemAsync(Key, request);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error on store.");
        }

        return true;
    }

    public async Task Clear()
    {
        using var asyncLock = await _lock.LockAsync();
        await _localStorageService.RemoveItemAsync(Key);
    }

    protected virtual void OnCollectionRelevantUpdate()
    {
        CollectionRelevantUpdate?.Invoke();
    }

    protected virtual void OnLazyUpdate()
    {
        LazyUpdate?.Invoke();
    }
}