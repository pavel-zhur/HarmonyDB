using HarmonyDB.Source.Api.Model.V1;
using OneShelf.Common;
using OneShelf.Common.Songs.FullTextSearch;

namespace OneShelf.Frontend.Web.Services;

public class SearchContext
{
    private readonly ILogger<SearchContext> _logger;
    private readonly DataProvider _dataProvider;
    private readonly InstantActions _instantActions;

    private DateTime? _lastRun;
    private readonly object _lastRunLockObject = new();
    private string? _lastRequested;

    public SearchContext(ILogger<SearchContext> logger, DataProvider dataProvider, InstantActions instantActions)
    {
        _logger = logger;
        _dataProvider = dataProvider;
        _instantActions = instantActions;
    }

    public event Func<(List<(string artist, string title, SearchHeader header)>? results, bool failed)?, Task>? Arrived;

    public (List<(string artist, string title, SearchHeader header)>? results, bool failed)? Peek { get; private set; }

    public string? PeekLastRequested() => _lastRequested;

    public void Update(string? query)
    {
        var searchQuery = GetSearchQuery(query);
        if (_lastRequested == searchQuery) return;

        if (searchQuery != null)
        {
            var result = _dataProvider.SearchPeek(searchQuery);
            if (result != null)
            {
                _lastRequested = searchQuery;
                var _ = OnArrived((HeadersToResult(result), false));
                return;
            }
        }
        else
        {
            _lastRequested = searchQuery;
            var _ = OnArrived(null);
            return;
        }

        Schedule(query);
    }

    public void RestartFailed()
    {
        _logger.LogInformation(nameof(RestartFailed));

        var lastRequested = _lastRequested;
        if (lastRequested == null) return;

        _lastRequested = null;
        _logger.LogInformation($"last requested: '{_lastRequested ?? "null"}'");

        Update(lastRequested);
    }

    private async void Schedule(string? query)
    {
        await Task.Yield();

        var searchQuery = GetSearchQuery(query);
        if (_lastRequested == searchQuery) return;

        await OnArrived(null);

        _lastRequested = searchQuery;
        _logger.LogInformation($"last requested: '{_lastRequested ?? "null"}'");
        if (searchQuery == null)
        {
            return;
        }
        
        await MaybeDelayAndRunNow();

        if (_lastRequested != searchQuery) return;

        List<SearchHeader> headers;
        try
        {
            _logger.LogInformation($"search: {searchQuery}");
            await _instantActions.VisitedSearch(searchQuery);
            headers = await _dataProvider.Search(searchQuery);
        }
        catch (Exception ex)
        {
            await OnArrived((null, true));
            _logger.LogError(ex, "Error on search.");
            return;
        }

        if (_lastRequested != searchQuery) return;

        var results = HeadersToResult(headers);

        await OnArrived((results, false));
    }

    private async Task MaybeDelayAndRunNow()
    {
        TimeSpan toWait;
        lock (_lastRunLockObject)
        {
            if (!_lastRun.HasValue)
            {
                _lastRun = DateTime.Now;
                return;
            }

            toWait = TimeSpan.FromMilliseconds(500) - (DateTime.Now - _lastRun.Value);
            _lastRun = DateTime.Now;
            if (toWait <= TimeSpan.Zero)
            {
                return;
            }
        }

        await Task.Delay(toWait);
    }

    private static string? GetSearchQuery(string? query) => query?.Trim();

    private static List<(string artist, string title, SearchHeader header)> HeadersToResult(List<SearchHeader> headers) =>
        headers
            .Select(s => (
                artist: s.Artists
                            ?.SelectSingle(x =>
                                string.Join(", ", x).SelectSingle(x => string.IsNullOrWhiteSpace(x) ? null : x)
                                    ?.Trim().ToLowerInvariant().ToSearchSyntax())
                        ?? "n/a",
                title: s.Title.SelectSingle(x =>
                    string.IsNullOrWhiteSpace(x) ? "n/a" : x.Trim().ToLowerInvariant().ToSearchSyntax()),
                s))
            .ToList();

    private async Task OnArrived((List<(string artist, string title, SearchHeader header)>? results, bool failed)? result)
    {
        Peek = result;
        _logger.LogInformation($"search results arrived, failed = {result?.failed}");
        await Task.WhenAll((Arrived?.GetInvocationList() ?? Enumerable.Empty<Delegate>())
            .Cast<Func<(List<(string artist, string title, SearchHeader header)>? results, bool failed)?, Task>>()
            .Select(x => x(result)));
    }
}