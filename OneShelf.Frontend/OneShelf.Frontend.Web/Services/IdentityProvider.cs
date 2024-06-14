using System.Text.Json;
using BlazorApplicationInsights.Interfaces;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Nito.AsyncEx;
using OneShelf.Authorization.Api.Model;
using OneShelf.Frontend.Web.Models;
using OneShelf.Frontend.Web.Pages;

namespace OneShelf.Frontend.Web.Services;

public class IdentityProvider
{
    private const string AuthKey = "authv2";

    private readonly ILogger<IdentityProvider> _logger;
    private readonly ILocalStorageService _localStorageService;
    private readonly NavigationManager _navigationManager;
    private readonly AsyncLock _lock = new();
    private readonly IApplicationInsights _appInsights;
    private readonly IJSRuntime _jsRuntime;

    private Identity? _identity;
    private bool _isInitialized;

    private int _appInsightsInitExceptions;
    private string? _appInsightsSetUserId;

    public IdentityProvider(ILogger<IdentityProvider> logger, ILocalStorageService localStorageService, NavigationManager navigationManager, IApplicationInsights appInsights, IJSRuntime jsRuntime)
    {
        _logger = logger;
        _localStorageService = localStorageService;
        _navigationManager = navigationManager;
        _appInsights = appInsights;
        _jsRuntime = jsRuntime;
    }

    public event Action IdentityChange;

    public Identity? Identity
    {
        get
        {
            if (!_isInitialized) throw new InvalidOperationException("Not initialized.");
            return _identity;
        }
    }

    public Identity RequiredIdentity
    {
        get
        {
            var identity = Identity;
            if (identity == null)
            {
                NavigateToLogin();
                throw new UnauthorizedException("No identity stored.");
            }

            return identity;
        }
    }

    public bool EnsureAuthorized()
    {
        if (Identity == null)
        {
            NavigateToLogin();
            return false;
        }

        return true;
    }

    private void NavigateToLogin()
    {
        _navigationManager.NavigateTo("/login");
    }

    public void NavigateToUserUnauthorizedIdentity()
    {
        _navigationManager.NavigateTo($"/user?{nameof(User.Unauthorized)}=true");
    }

    public async Task Set(object? user)
    {
        using var _ = await _lock.LockAsync();

        if (user != null)
        {
            var data = user.ToString();
            _identity = JsonSerializer.Deserialize<Identity>(data!)!;
            await _localStorageService.SetItemAsStringAsync(AuthKey, data);
        }
        else
        {
            _identity = null;
            await _localStorageService.RemoveItemAsync(AuthKey);
        }

        await SetApplicationInsights();
        OnIdentityChange();
    }

    public async Task Initialize()
    {
        if (_isInitialized) throw new InvalidOperationException("Already initialized.");

        var value = await _localStorageService.GetItemAsStringAsync(AuthKey);
        if (value != null)
        {
            _identity = JsonSerializer.Deserialize<Identity>(value)!;
            await SetApplicationInsights();
        }

        _isInitialized = true;
    }

    private async Task SetApplicationInsights()
    {
        try
        {
            var authenticatedUserId = _identity?.Id.ToString();
            if (authenticatedUserId == _appInsightsSetUserId) return;
            _appInsightsSetUserId = authenticatedUserId;

            _appInsights.InitJSRuntime(_jsRuntime);

            if (authenticatedUserId != null)
            {
                await _appInsights.SetAuthenticatedUserContext(authenticatedUserId);
            }
            else
            {
                await _appInsights.ClearAuthenticatedUserContext();
            }
        }
        catch (Exception e)
        {
            _appInsightsInitExceptions++;

            if (_appInsightsInitExceptions < 5)
            {
                _logger.LogError(e, "Error setting the application insights user.");
            }
        }
    }

    protected virtual void OnIdentityChange()
    {
        IdentityChange?.Invoke();
    }
}