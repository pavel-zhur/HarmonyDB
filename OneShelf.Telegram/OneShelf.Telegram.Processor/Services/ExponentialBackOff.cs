using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OneShelf.Telegram.Processor.Services;

public class ExponentialBackOff
{
    private readonly ILogger<ExponentialBackOff> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    public ExponentialBackOff(ILogger<ExponentialBackOff> logger, IHostApplicationLifetime hostApplicationLifetime)
    {
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
    }

    public async Task WithRetry(Func<Task> task)
    {
        await WithRetry(async () =>
        {
            await task();
            return (object)null!;
        });
    }
    public async Task<T> WithRetry<T>(Func<Task<T>> task)
    {
        TimeSpan delay = TimeSpan.FromSeconds(3);
        while (true)
        {
            try
            {
                return await task();
            }
            catch (Exception e) when (e.Message.Contains("Too Many Requests"))
            {
                _logger.LogInformation("Too many requests, retry after {} seconds", (int)delay.TotalSeconds);
                _logger.LogTrace(e, "Too many requests exception");
                await Task.Delay(delay, _hostApplicationLifetime.ApplicationStopping);
            }
        }
    }
}