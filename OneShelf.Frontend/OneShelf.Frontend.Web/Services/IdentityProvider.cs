using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
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

    private Identity? _identity;
    private bool _isInitialized;

    public IdentityProvider(ILogger<IdentityProvider> logger, ILocalStorageService localStorageService, NavigationManager navigationManager)
    {
        _logger = logger;
        _localStorageService = localStorageService;
        _navigationManager = navigationManager;
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

        _logger.LogInformation($"idp identity = {Identity}");

        OnIdentityChange();
    }

    public async Task Initialize()
    {
        if (_isInitialized) throw new InvalidOperationException("Already initialized.");

        var value = await _localStorageService.GetItemAsStringAsync(AuthKey);
        if (value != null)
        {
            _identity = JsonSerializer.Deserialize<Identity>(value)!;
        }

        _isInitialized = true;
    }

    protected virtual void OnIdentityChange()
    {
        IdentityChange?.Invoke();
    }
}