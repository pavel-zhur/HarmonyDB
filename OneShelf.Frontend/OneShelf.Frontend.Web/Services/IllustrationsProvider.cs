using Blazored.LocalStorage;
using Nito.AsyncEx;
using OneShelf.Frontend.Api.Model.V3.Illustrations;
using OneShelf.Frontend.Web.Models;

namespace OneShelf.Frontend.Web.Services;

public class IllustrationsProvider
{
    private const string IllustrationsKey = "IllustrationsProviderV3";
    private static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromHours(1);
    private static readonly TimeSpan AttemptDelaySeconds = TimeSpan.FromSeconds(5);

    private readonly ILogger<IllustrationsProvider> _logger;
    private readonly ILocalStorageService _localStorageService;
    private readonly Api _api;

    private readonly AsyncLock _loading = new();

    private IllustrationsCache? _data;
    private bool _localStorageRead;
    private DateTime? _lastRequested;

    public IllustrationsProvider(ILogger<IllustrationsProvider> logger, ILocalStorageService localStorageService, Api api)
    {
        _logger = logger;
        _localStorageService = localStorageService;
        _api = api;

        MaybeInit();
    }

    public AllIllustrations? Peek()
    {
        MaybeInit();
        return _data?.All;
    }

    public async Task<AllIllustrations?> Get()
    {
        if (DateTime.Now - _data?.ReceivedOn > RefreshDelay)
        {
            Refresh();
        }

        var data = _data;
        if (data != null)
        {
            return data.All;
        }

        using var _ = await _loading.LockAsync();
        try
        {
            if (_data != null)
            {
                return _data.All;
            }

            if (!_localStorageRead)
            {
                _data = await _localStorageService.GetItemAsync<IllustrationsCache>(IllustrationsKey);
                if (_data != null)
                {
                    return _data?.All;
                }

                _localStorageRead = true;
            }

            await Load();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error loading illustrations.");
            return null;
        }

        return _data?.All;
    }

    private async void Refresh()
    {
        using var _ = await _loading.LockAsync();
        await Load();
    }

    private async Task Load()
    {
        if ((DateTime.Now - _lastRequested) < AttemptDelaySeconds)
        {
            return;
        }

        try
        {
            var loaded = await _api.GetIllustrations(_data?.All.Etag, true);
            _data =
                loaded.Illustrations == null
                    ? _data?.All == null
                        ? null
                        : new()
                        {
                            All = _data.All,
                            ReceivedOn = DateTime.Now,
                        }
                    : new()
                    {
                        All = loaded.Illustrations,
                        ReceivedOn = DateTime.Now,
                    };
            await _localStorageService.SetItemAsync(IllustrationsKey, _data);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error refreshing illustrations.");
        }
        finally
        {
            _lastRequested = DateTime.Now;
        }
    }

    public async void MaybeInit()
    {
        await Task.Delay(FirstDelay);
        try
        {
            await Get();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error on first init.");
        }
    }

    public async Task Clear()
    {
        using var _ = await _loading.LockAsync();
        await _localStorageService.RemoveItemAsync(IllustrationsKey);
        _localStorageRead = true;
        _data = null;
    }
}