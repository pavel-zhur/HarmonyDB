using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Nito.AsyncEx;
using OneShelf.Authorization.Api.Model;
using OneShelf.Frontend.Api.Model.V3.Api;
using OneShelf.Frontend.Api.Model.V3.Databasish;
using OneShelf.Frontend.Web.Models;

namespace OneShelf.Frontend.Web.Services;

public class CollectionIndexProvider : IDisposable
{
    private const string CollectionKey = "CollectionV4";
    private static readonly TimeSpan EmptyFrequency = TimeSpan.FromSeconds(1.5);

    private readonly ILogger<CollectionIndexProvider> _logger;
    private readonly IdentityProvider _identityProvider;
    private readonly ILocalStorageService _localStorageService;
    private readonly Api _api;
    private readonly NavigationManager _navigationManager;

    private readonly AsyncLock _syncLock = new();
    private readonly AsyncLock _reapplicationLock = new();

    private DateTime _lastActuallyLoaded = DateTime.MinValue;
    private (CollectionIndex? collectionIndex, bool failed) _collectionIndex;
    private readonly InstantActions _instantActions;
    private Collection? _storedCollection;
    private Guid _recentLazy;

    private bool _syncedFirst;

    public CollectionIndexProvider(ILogger<CollectionIndexProvider> logger, IdentityProvider identityProvider, ILocalStorageService localStorageService, Api api, InstantActions instantActions, NavigationManager navigationManager)
    {
        _logger = logger;
        _identityProvider = identityProvider;
        _localStorageService = localStorageService;
        _api = api;
        _instantActions = instantActions;
        _navigationManager = navigationManager;

        navigationManager.LocationChanged += OnNavigationManagerOnLocationChanged;

        _instantActions.CollectionRelevantUpdate += InstantActionHappened;
        _instantActions.LazyUpdate += LazyActionHappened;

        MightSync();
    }

    public event Func<CollectionIndex?, bool, Task>? CollectionChanged;

    public event Action SyncingChanged;

    public bool IsSyncing { get; private set; }

    public (CollectionIndex? collectionIndex, bool failed) Peek() => _collectionIndex;

    public async void MightSync()
    {
        await Task.Delay(EmptyFrequency);
        if (_identityProvider.Identity != null && !_syncedFirst)
        {
            Sync();
            _syncedFirst = true;
        }
    }

    private async void LazyActionHappened()
    {
        var remember = _recentLazy = Guid.NewGuid();
        await Task.Delay(EmptyFrequency);
        if (remember != _recentLazy) return;

        try
        {
            Sync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error on keep alive");
        }
    }

    private async void InstantActionHappened()
    {
        // take instant actions
        // reapply them to the collection
        // notify about the collection change - if any changes
        // sync

        using (await _reapplicationLock.LockAsync())
        {
            if (!_identityProvider.EnsureAuthorized())
            {
                return;
            }

            var identity = _identityProvider.RequiredIdentity;

            var started = DateTime.Now;
            _storedCollection ??= await _localStorageService.GetItemAsync<Collection>(CollectionKey);
            Console.WriteLine($"{nameof(InstantActionHappened)} ({(DateTime.Now - started).TotalMilliseconds} ms)");
            if (_storedCollection != null)
            {
                var peek = await _instantActions.Peek();
                var collectionIndexFromStorage = new CollectionIndex(_storedCollection, identity.Id, peek);
                _collectionIndex = (collectionIndexFromStorage, false);
                await OnCollectionChanged(collectionIndexFromStorage, false);
            }
        }

        Sync();
    }

    public async void Sync(bool force = false)
    {
        await Task.Yield();
        var syncStarted = DateTime.Now;
        try
        {
            using (await _syncLock.LockAsync())
            {
                IsSyncing = true;
                OnSyncingChanged();
                ApplyRequest actions;
                Identity identity;
                using (await _reapplicationLock.LockAsync())
                {
                    if (!_identityProvider.EnsureAuthorized())
                    {
                        if (_collectionIndex == (null, true)) return;

                        _collectionIndex = (null, true);
                        await OnCollectionChanged(null, true);
                        return;
                    }

                    identity = _identityProvider.RequiredIdentity;

                    // if failed, switch to loading and notify (if present, do nothing)
                    if (_collectionIndex.collectionIndex == null)
                    {
                        var started = DateTime.Now;
                        _storedCollection ??= await _localStorageService.GetItemAsync<Collection>(CollectionKey);
                        Console.WriteLine($"{nameof(Sync)}.1 ({(DateTime.Now - started).TotalMilliseconds} ms)");
                        var peek = await _instantActions.Peek();
                        if (_storedCollection != null)
                        {
                            var collectionIndexFromStorage = new CollectionIndex(_storedCollection, identity.Id, peek);
                            _collectionIndex = (collectionIndexFromStorage, false);
                            await OnCollectionChanged(collectionIndexFromStorage, false);
                        }
                        else // currently is failed. cannot be loading since we're inside the lock
                        {
                            _collectionIndex = (null, false); // make non-failed
                            await OnCollectionChanged(null, false);
                        }
                    }

                    actions = await _instantActions.Peek();
                }

                if (!actions.GetIds().Any()
                    && DateTime.Now - _lastActuallyLoaded < EmptyFrequency
                    && _collectionIndex.collectionIndex != null
                    && !force)
                {
                    IsSyncing = false;
                    OnSyncingChanged();
                    return;
                }

                Collection collection;
                try
                {
                    actions.Identity = identity;
                    var applying = DateTime.Now;
                    collection = await _api.Apply(actions, _storedCollection);
                    syncStarted += (DateTime.Now - applying);
                    _lastActuallyLoaded = DateTime.Now;
                    IsSyncing = false;
                    OnSyncingChanged();
                }
                catch (Exception e)
                {
                    IsSyncing = false;
                    OnSyncingChanged();
                    using (await _reapplicationLock.LockAsync())
                    {
                        _logger.LogError(e,
                            "Error applying the pending actions. Hopefully, an internet connection error.");

                        if (_collectionIndex.collectionIndex == null)
                        {
                            _collectionIndex = (null, true);
                            await OnCollectionChanged(null, true);
                        }

                        return;
                    }
                }

                using (await _reapplicationLock.LockAsync())
                {
                    await _instantActions.ExtractSent(actions);

                    if (_storedCollection == null || collection != _storedCollection && (collection.Etag != _storedCollection.Etag || !collection.DeepHash().SequenceEqual(_storedCollection.DeepHash())))
                    {
                        var started = DateTime.Now;
                        await _localStorageService.SetItemAsync(CollectionKey, collection);
                        _storedCollection = collection;
                        Console.WriteLine($"{nameof(Sync)}.2.2 ({(DateTime.Now - started).TotalMilliseconds} ms)");
                    }

                    var collectionIndex = new CollectionIndex(collection, identity.Id, await _instantActions.Peek());

                    if (_collectionIndex.collectionIndex?.DeepEquals(collectionIndex) != true)
                    {
                        _logger.LogInformation(
                            "Ehh, collection index changed after it's back from the server. If a concurrent action happened, it's fine, otherwise there's a mismatch between the local application logic and the server application logic. Couldn't avoid the re-render.");
                        _collectionIndex = (collectionIndex, false);
                        await OnCollectionChanged(collectionIndex, false);
                    }
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception refreshing collection index (asynchronous background task).");
            _collectionIndex = (null, true);
            try
            {
                await OnCollectionChanged(null, true);
            }
            catch (Exception e2)
            {
                _logger.LogError(e2, "Error notifying about the collection change.");
            }
        }
        finally
        {
            Console.WriteLine($"{nameof(Sync)} ({(DateTime.Now - syncStarted).TotalMilliseconds} ms)");
        }
    }

    public async Task Clear(bool withReload = true)
    {
        using (await _reapplicationLock.LockAsync())
        {
            await _localStorageService.RemoveItemAsync(CollectionKey);
            _storedCollection = null;
            _collectionIndex = (null, !withReload);
            await OnCollectionChanged(null, !withReload);
        }

        if (withReload)
        {
            Sync();
        }
    }

    private async Task OnCollectionChanged(CollectionIndex? collectionIndex, bool failed)
    {
        var started = DateTime.Now;
        await Task.WhenAll(CollectionChanged?.GetInvocationList().Cast<Func<CollectionIndex?, bool, Task>>().Select(x => x(collectionIndex, failed)) ?? Enumerable.Empty<Task>());
        Console.WriteLine($"{nameof(OnCollectionChanged)} ({(DateTime.Now - started).TotalMilliseconds} ms)");
    }

    public void Dispose()
    {
        _instantActions.CollectionRelevantUpdate -= InstantActionHappened;
        _instantActions.LazyUpdate -= LazyActionHappened;

        _navigationManager.LocationChanged += OnNavigationManagerOnLocationChanged;
    }

    private void OnNavigationManagerOnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (_identityProvider.Identity != null)
        {
            LazyActionHappened();
        }
    }

    protected virtual void OnSyncingChanged()
    {
        SyncingChanged?.Invoke();
    }
}