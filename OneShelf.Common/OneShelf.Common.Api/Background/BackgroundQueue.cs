using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OneShelf.Common.Api.Background;

/// <summary>
/// The processing of one item of a <see cref="BackgroundQueue{TItem}"/>, created in a scope of its own for every attempt.
/// </summary>
public interface IBackgroundWork<in TItem>
{
    Task Process(TItem item, CancellationToken cancellationToken);
}

public record BackgroundQueueOptions
{
    /// <summary>How many items are processed at a time.</summary>
    public required int MaxConcurrency { get; init; }

    /// <summary>How many times an item is tried before it is given up.</summary>
    public required int MaxAttempts { get; init; }

    /// <summary>How long a failed item waits before it is tried again.</summary>
    public required TimeSpan RetryDelay { get; init; }
}

/// <summary>
/// Items accepted now and processed in the background of the application, as messages of a storage queue were.
/// The items live in memory only: what is in the queue when the application stops is lost.
/// </summary>
public sealed class BackgroundQueue<TItem>
{
    internal Channel<(TItem item, int attempt)> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<(TItem item, int attempt)>();

    public void Enqueue(TItem item)
    {
        if (!Channel.Writer.TryWrite((item, 1))) throw new("The queue is closed.");
    }
}

internal sealed class BackgroundQueueService<TItem, TWork> : BackgroundService
    where TWork : class, IBackgroundWork<TItem>
{
    private readonly BackgroundQueue<TItem> _queue;
    private readonly BackgroundQueueOptions _options;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<BackgroundQueueService<TItem, TWork>> _logger;

    public BackgroundQueueService(BackgroundQueue<TItem> queue, BackgroundQueueOptions options, IServiceScopeFactory serviceScopeFactory, ILogger<BackgroundQueueService<TItem, TWork>> logger)
    {
        _queue = queue;
        _options = options;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.WhenAll(Enumerable.Range(0, _options.MaxConcurrency).Select(_ => Consume(stoppingToken)));

    private async Task Consume(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var (item, attempt) in _queue.Channel.Reader.ReadAllAsync(stoppingToken))
            {
                await Process(item, attempt, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task Process(TItem item, int attempt, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<TWork>().Process(item, stoppingToken);
        }
        catch (Exception e)
        {
            if (attempt >= _options.MaxAttempts)
            {
                _logger.LogError(e, "{work} failed {attempts} times, the item is given up.", typeof(TWork).Name, attempt);
                return;
            }

            _logger.LogWarning(e, "{work} failed (attempt {attempt}), the item will be tried again in {delay}.", typeof(TWork).Name, attempt, _options.RetryDelay);
            _ = Retry(item, attempt + 1, stoppingToken);
        }
    }

    private async Task Retry(TItem item, int attempt, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(_options.RetryDelay, stoppingToken);
            await _queue.Channel.Writer.WriteAsync((item, attempt), stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }
}

public static class BackgroundQueueServiceCollectionExtensions
{
    /// <summary>
    /// A <see cref="BackgroundQueue{TItem}"/> whose items are processed by <typeparamref name="TWork"/>.
    /// </summary>
    public static IServiceCollection AddBackgroundQueue<TItem, TWork>(this IServiceCollection services, BackgroundQueueOptions options)
        where TWork : class, IBackgroundWork<TItem>
        => services
            .AddSingleton<BackgroundQueue<TItem>>()
            .AddScoped<TWork>()
            .AddHostedService(sp => new BackgroundQueueService<TItem, TWork>(
                sp.GetRequiredService<BackgroundQueue<TItem>>(),
                options,
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<BackgroundQueueService<TItem, TWork>>>()));
}
