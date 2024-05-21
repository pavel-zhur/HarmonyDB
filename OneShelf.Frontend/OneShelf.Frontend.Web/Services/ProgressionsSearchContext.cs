using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Services;
using OneShelf.Common;
using OneShelf.Frontend.Web.Models;

namespace OneShelf.Frontend.Web.Services;

public class ProgressionsSearchContext
{
    private readonly ILogger<ProgressionsSearchContext> _logger;
    private readonly ProgressionsCacheLoader _progressionsCacheLoader;
    private readonly ProgressionsSearch _progressionsSearch;

    public ProgressionsSearchContext(ILogger<ProgressionsSearchContext> logger, ProgressionsCacheLoader progressionsCacheLoader, ProgressionsSearch progressionsSearch)
    {
        _logger = logger;
        _progressionsCacheLoader = progressionsCacheLoader;
        _progressionsSearch = progressionsSearch;
    }

    public event Action<ProgressionsSearchSource>? SearchResultsChanged;

    public (Dictionary<ChordsProgression, float> foundProgressionsWithCoverage, Dictionary<HarmonyGroup, bool> harmonyGroupsWithIsFirst, string title)? Results { get; private set; }

    public void Clear(ProgressionsSearchSource source)
    {
        Results = null;
        OnSearchChanged(source);
    }

    public async void Start(ProgressionsSearchSource source, HarmonyMovementsSequence sequence)
    {
        Results = null;

        try
        {
            var progressions = await _progressionsCacheLoader.Get();
            if (progressions == null)
            {
                throw new("Could not have happened.");
            }

            Results = _progressionsSearch.Search(progressions.Select(x => x.progression).ToList(), sequence)
                .SelectSingle(x => (
                    x.foundProgressionsWithCoverage,
                    x.harmonyGroupsWithIsFirst, 
                    string.Join(" - ", sequence.Movements.Select(x => x.To).Prepend(sequence.Movements.First().From).Select(x => x.HarmonyRepresentation))));
            OnSearchChanged(source);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Search initiation failed.");
            Results = null;
        }
    }

    private void OnSearchChanged(ProgressionsSearchSource source)
    {
        SearchResultsChanged?.Invoke(source);
    }
}