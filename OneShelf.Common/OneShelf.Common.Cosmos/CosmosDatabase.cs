using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.Cosmos;
using OneShelf.Common.Cosmos.Options;
using System.Threading;
using Nito.AsyncEx;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OneShelf.Common.Cosmos;

public abstract class CosmosDatabase : IDisposable
{
    protected readonly CosmosDatabaseOptions _options;
    protected readonly CosmosClient _client;
    protected readonly AsyncLock _lock = new();
    protected readonly ILogger<CosmosDatabase> _logger;

    protected CosmosDatabase(CosmosDatabaseOptions options, ILogger<CosmosDatabase> logger)
    {
        _options = options;
        _logger = logger;

        _client = new(options.EndPointUri, options.PrimaryKey, new()
        {
            SerializerOptions = new()
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
            },
            AllowBulkExecution = true,
        });
    }

    public const string IllegalIdCharacters = "/\\#?";
    
    public void Dispose() => _client.Dispose();

    protected void ValidateBeforeSaving(IValidatableObject validatableObject)
    {
        Validator.ValidateObject(validatableObject, new(validatableObject));
    }

    protected async Task Multiple2<T>(IReadOnlyCollection<T> items, Func<T, Task> action)
    {
        await Task.WhenAll(items.Select(action));
    }

    protected async Task MultipleWithRetry<T>(IReadOnlyCollection<T> items, Func<T, CancellationToken, Task> action, int threads = 5, bool retryTimeoutsToo = false)
    {
        var failures = 0;
        var delay = TimeSpan.FromMilliseconds(10);

        // todo: think if it's good or not. https://stackoverflow.com/a/70191638 fall fast principle is applied
        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = threads }, async (item, cancellationToken) =>
        {
            while (true)
            {
                try
                {
                    await action(item, cancellationToken);
                    break;
                }
                catch (CosmosException e) when (e.Message.Contains("(429)") || e.Message.Contains("(408)") && retryTimeoutsToo)
                {
                    Interlocked.Increment(ref failures);

                    if (failures >= 18)
                    {
	                    throw new($"With retry upsert cosmos failed: {failures} failures, {typeof(T).FullName} type.");
                    }

					await Task.Delay(delay, cancellationToken);
                    delay *= 1.5 + Random.Shared.Next(-15, 15) / 100f;
                }
            }
        });

        if (failures > 0)
        {
            _logger.LogWarning("Upsert cosmos: {threads} threads, {count} items, {failures} failures, {type} type.", threads, items.Count, failures, typeof(T).FullName);
        }
    }
    
    protected async Task<T> WithRetry<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default, bool retryTimeoutsToo = false)
    {
        var failures = 0;
        var delay = TimeSpan.FromMilliseconds(10);
        T result;
        while (true)
        {
            try
            {
                result = await action(cancellationToken);
                break;
            }
            catch (CosmosException e) when (e.Message.Contains("(429)") || e.Message.Contains("(408)") && retryTimeoutsToo)
            {
                Interlocked.Increment(ref failures);

                if (failures >= 18)
                {
	                throw new($"With retry cosmos failed: {failures} failures, {typeof(T).FullName} type.");
                }

				await Task.Delay(delay, cancellationToken);
                delay *= 1.5 + Random.Shared.Next(-15, 15) / 100f;
            }
        }

        if (failures > 0)
        {
            _logger.LogWarning("With retry cosmos: {failures} failures, {type} type.", failures, typeof(T).FullName);
        }

        return result;
    }

    protected async Task UpsertMultiple2<T>(Container container, IReadOnlyCollection<T> items)
        where T : IValidatableObject
    {
        foreach (var item in items)
        {
            ValidateBeforeSaving(item);
        }

        await Multiple2(items, item => container.UpsertItemAsync(item));
    }

    protected async Task DeleteMultiple2<T>(Container container, IReadOnlyCollection<string> items)
        where T : IValidatableObject
    {
        await Multiple2(items, item => container.DeleteItemAsync<T>(item, new(item)));
    }

    protected async Task UpsertMultipleWithRetry<T>(Container container, IReadOnlyCollection<T> items, int threads = 5)
        where T : IValidatableObject
    {
        foreach (var item in items)
        {
            ValidateBeforeSaving(item);
        }

        await MultipleWithRetry(items,
            (item, cancellationToken) => container.UpsertItemAsync(item, cancellationToken: cancellationToken),
            threads);
    }

    protected async Task DeleteMultipleWithRetry<T>(Container container, IReadOnlyCollection<string> items, int threads = 5)
        where T : IValidatableObject
    {
        await MultipleWithRetry(items,
            (item, cancellationToken) => container.DeleteItemAsync<T>(item, new(item), cancellationToken: cancellationToken),
            threads);
    }
}