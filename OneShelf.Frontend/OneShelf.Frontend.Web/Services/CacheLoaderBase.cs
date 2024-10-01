namespace OneShelf.Frontend.Web.Services;

public abstract class CacheLoaderBase : IDisposable
{
    public const int MissingChordsThresholdForProgressionsSlowFail = 30;
    public const int MissingChordsThresholdForProgressionsOnlyFastFail = 50;
    public const int MissingProgressionsThreshold = 20;

    protected readonly ILogger<CacheLoaderBase> Logger;

    private readonly object _startStopLockObject = new();
    private CancellationTokenSource? _runningTokenSource;
    private int? _lastProgress;

    protected CacheLoaderBase(ILogger<CacheLoaderBase> logger)
    {
        Logger = logger;
    }

    public event Func<IReadOnlyCollection<string>?, Task>? Updated;

    public event Action<int?>? ProgressChanged;

    public bool IsRunning => _runningTokenSource != null;

    public void Start()
    {
        lock (_startStopLockObject)
        {
            if (_runningTokenSource != null)
            {
                return;
            }

            _runningTokenSource = new();
            Go(_runningTokenSource.Token);
        }
    }

    public void Stop()
    {
        lock (_startStopLockObject)
        {
            OnProgressChanged(null);

            if (_runningTokenSource == null)
            {
                return;
            }

            _runningTokenSource.Cancel();
            _runningTokenSource.Dispose();
            _runningTokenSource = null;
        }
    }

    private async Task OnUpdated(IReadOnlyCollection<string>? availableExternalIds)
    {
        await Task.WhenAll(Updated?.GetInvocationList().Cast<Func<IReadOnlyCollection<string>?, Task>>().Select(x => x(availableExternalIds)) ?? Enumerable.Empty<Task>());
    }

    private void OnProgressChanged(int? progress)
    {
        if (_lastProgress == progress) return;
        _lastProgress = progress;

        try
        {
            ProgressChanged?.Invoke(progress);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Progress changed invocation error.");
        }
    }

    protected async Task OnFinished()
    {
        await OnUpdated(null);
        OnProgressChanged(null);
    }

    protected async Task OnUpdated(IReadOnlyCollection<string> availableExternalIds, int progress)
    {
        await OnUpdated(availableExternalIds);
        OnProgressChanged(progress);
    }

    public virtual void Dispose()
    {
        Stop();
    }

    private async void Go(CancellationToken token)
    {
        Logger.LogInformation("Chords loading started.");

        try
        {
            await Work(token);
            OnProgressChanged(null);
            Logger.LogInformation("Chords loading finished successfully.");
        }
        catch (TaskCanceledException)
        {
            _runningTokenSource?.Dispose();
            _runningTokenSource = null;
            OnProgressChanged(null);
            return;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error during background cache loading.");
        }

        lock (_startStopLockObject)
        {
            _runningTokenSource?.Dispose();
            _runningTokenSource = null;
        }

        await OnUpdated(null);
    }

    protected abstract Task Work(CancellationToken token);
}