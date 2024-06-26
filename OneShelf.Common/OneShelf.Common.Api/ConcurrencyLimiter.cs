using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OneShelf.Common.Api;

public class ConcurrencyLimiter
{
    private readonly ILogger<ConcurrencyLimiter> _logger;
    private readonly ConcurrencyLimiterOptions _options;
    
    private volatile int _current;

    public ConcurrencyLimiter(IOptions<ConcurrencyLimiterOptions> options, ILogger<ConcurrencyLimiter> logger)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<TResult> ExecuteOrThrow<TResult>(Func<Task<TResult>> taskGetter)
    {
        if (_current > _options.MaxConcurrency)
            throw new ServiceConcurrencyException();

        try
        {
            var value = Interlocked.Increment(ref _current);
            _logger.LogInformation("Request concurrency: {concurrency}.", value);
            if (value > _options.MaxConcurrency)
            {
                throw new ServiceConcurrencyException();
            }

            return await taskGetter();
        }
        finally
        {
            Interlocked.Decrement(ref _current);
        }
    }
}