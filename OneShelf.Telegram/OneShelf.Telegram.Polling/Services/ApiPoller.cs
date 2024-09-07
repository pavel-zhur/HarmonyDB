using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Polling.Model;
using Telegram.BotAPI;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.Polling.Services;

public class ApiPoller : BackgroundService
{
    private readonly ILogger<ApiPoller> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PollerOptions _pollerOptions;

    private readonly TelegramBotClient? _api;

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
                    Console.WriteLine(JsonSerializer.Serialize(update, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, }));
                    try
                    {
                        using var httpClient = _httpClientFactory.CreateClient();
                        httpClient.DefaultRequestHeaders.Add(_pollerOptions.WebHooksSecretHeader, _pollerOptions.WebHooksSecretToken);
                        await httpClient.PostAsync(_pollerOptions.IncomingUpdateUrl, new StringContent(
                            JsonSerializer.Serialize(update, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, }), Encoding.UTF8), stoppingToken);
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

        async Task<List<Update>> GetUpdatesSafeAsync(
            [Optional] int? offset)
        {
            try
            {
                return (await _api.GetUpdatesAsync(offset, timeout: 30, cancellationToken: stoppingToken)).ToList();
            }
            catch (TaskCanceledException)
            {
                return [];
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GetUpdates");
                return [];
            }
        }
    }
}