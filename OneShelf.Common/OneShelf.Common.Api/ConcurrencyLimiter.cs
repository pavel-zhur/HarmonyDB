using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OneShelf.Common.Api;

public class ConcurrencyLimiter
{
    private readonly ILogger<ConcurrencyLimiter> _logger;
    private readonly ConcurrencyLimiterOptions _options;

    private readonly Dictionary<Type, int> _counters = new();

    public ConcurrencyLimiter(IOptions<ConcurrencyLimiterOptions> options, ILogger<ConcurrencyLimiter> logger)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<TResult> ExecuteOrThrow<TResult>(Func<Task<TResult>> taskGetter)
    {
        if (_counters.GetValueOrDefault(typeof(TResult)) > _options.MaxConcurrency)
            throw new ServiceConcurrencyException();

        try
        {
            int value;
            lock (_counters)
            {
                var current = _counters.GetValueOrDefault(typeof(TResult));
                value = _counters[typeof(TResult)] = current + 1;
            }

            if (value > _options.MaxConcurrency)
            {
                throw new ServiceConcurrencyException();
            }

            _logger.LogInformation("Request concurrency: {concurrency}.", value);

            return await taskGetter();
        }
        finally
        {
            lock (_counters)
            {
                _counters[typeof(TResult)]--;
            }
        }
    }
}