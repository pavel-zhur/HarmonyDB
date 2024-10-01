using Microsoft.JSInterop;

namespace OneShelf.Frontend.Web.Services;

public class JsFunctions
{
    private readonly ILogger<JsFunctions> _logger;
    private readonly IJSRuntime _jsRuntime;

    public JsFunctions(ILogger<JsFunctions> logger, IJSRuntime jsRuntime)
    {
        _logger = logger;
        _jsRuntime = jsRuntime;
    }

    public async Task<string?> Prompt(string query)
    {
        var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", $"./services-js/{GetType().Name}.js");
        return await module.InvokeAsync<string?>("Prompt", query);
    }

    public async Task<bool> Confirm(string query)
    {
        var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", $"./services-js/{GetType().Name}.js");
        return await module.InvokeAsync<bool>("Confirm", query);
    }

    public async Task ScrollToTop()
    {
        var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", $"./services-js/{GetType().Name}.js");
        await module.InvokeVoidAsync("ScrollToTop");
    }

    public async Task FirstInitSuccessful()
    {
        var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", $"./services-js/{GetType().Name}.js");
        await module.InvokeVoidAsync("FirstInitSuccessful");
    }
}