using Blazored.LocalStorage;
using OneShelf.Frontend.Web.Models;

namespace OneShelf.Frontend.Web.Services;

public class Preferences
{
    private readonly ILocalStorageService _localStorageService;

    private const string CofKey = "cof";
    private const string ZoomKey = "zoom";
    private const string PrintPreferencesKey = "ppv1";

    private bool? _circleOfFifthsVisible;
    private int? _zoom;
    private PrintPreferences? _printPreferences;

    public Preferences(ILocalStorageService localStorageService)
    {
        _localStorageService = localStorageService;
    }

    public async Task<bool> IsCircleOfFifthsVisible()
    {
        if (_circleOfFifthsVisible.HasValue) return _circleOfFifthsVisible.Value;

        _circleOfFifthsVisible = (await _localStorageService.GetItemAsStringAsync(CofKey))?.Length > 0;

        return _circleOfFifthsVisible.Value;
    }

    public async Task CircleOfFifthsVisible(bool isVisible)
    {
        _circleOfFifthsVisible = isVisible;
        if (isVisible)
        {
            await _localStorageService.SetItemAsStringAsync(CofKey, "yes");
        }
        else
        {
            await _localStorageService.RemoveItemAsync(CofKey);
        }
    }

    public async Task<int> GetZoom()
    {
        if (_zoom.HasValue) return _zoom.Value;

        _zoom = int.TryParse(await _localStorageService.GetItemAsStringAsync(ZoomKey), out var value) ? value : 100;

        return _zoom.Value;
    }

    public async Task SetZoom(int zoom)
    {
        _zoom = zoom;

        await _localStorageService.SetItemAsStringAsync(ZoomKey, zoom.ToString());
    }

    public async Task<PrintPreferences> GetPrintPreferences()
    {
        if (_printPreferences != null) return _printPreferences;

        _printPreferences = await _localStorageService.GetItemAsync<PrintPreferences>(PrintPreferencesKey);

        if (_printPreferences == null)
        {
            _printPreferences = new();
            await _localStorageService.SetItemAsync(PrintPreferencesKey, _printPreferences);
        }

        return _printPreferences;
    }

    public async Task SetPrintPreferences(PrintPreferences printPreferences)
    {
        _printPreferences = printPreferences;
        await _localStorageService.SetItemAsync(PrintPreferencesKey, printPreferences);
    }
}