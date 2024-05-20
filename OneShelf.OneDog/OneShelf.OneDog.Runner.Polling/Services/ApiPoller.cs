using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OneShelf.OneDog.Runner.Polling.Model;
using Telegram.BotAPI;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDog.Runner.Polling.Services;

public class ApiPoller : BackgroundService
{
    private readonly ILogger<ApiPoller> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PollerOptions _pollerOptions;

    private readonly BotClient? _api;

    public ApiPoller(IOptions<PollerOptions> pollerOptions, ILogger<ApiPoller> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _pollerOptions = pollerOptions.Value;

        _api = new(_pollerOptions.Token);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var updates = await GetUpdatesSafeAsync();
        while (!stoppingToken.IsCancellationRequested)
        {
            if (updates.Any())
            {
                foreach (var update in updates)
                {
                    Console.WriteLine(JsonConvert.SerializeObject(update, Formatting.Indented));
                    try
                    {
                        using var httpClient = _httpClientFactory.CreateClient();
                        httpClient.DefaultRequestHeaders.Add(_pollerOptions.WebHooksSecretHeader, _pollerOptions.WebHooksSecretToken);
                        await httpClient.PostAsync(_pollerOptions.IncomingUpdateUrl, new StringContent(
                            JsonConvert.SerializeObject(update), Encoding.UTF8), stoppingToken);
                        _logger.LogInformation("Successfully forwarded.");
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error sending the update to the functions.");
                    }
                }

                var offset = updates.Last().UpdateId + 1;
                updates = await GetUpdatesSafeAsync(offset);
            }
            else
            {
                updates = await GetUpdatesSafeAsync();
            }
        }

        async Task<Update[]> GetUpdatesSafeAsync(
            [Optional] int? offset)
        {
            try
            {
                return await _api.GetUpdatesAsync(offset, timeout: 30, cancellationToken: stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return Array.Empty<Update>();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GetUpdates");
                return Array.Empty<Update>();
            }
        }
    }
}