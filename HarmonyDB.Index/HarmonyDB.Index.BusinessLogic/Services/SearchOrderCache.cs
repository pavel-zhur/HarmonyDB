using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;

namespace HarmonyDB.Index.BusinessLogic.Services;

public class SearchOrderCache : MemoryCacheBase<(IndexHeaders indexHeaders, IReadOnlyList<string> progressionsExternalIds), IReadOnlyList<string>>
{
    private readonly ProgressionsCache _progressionsCache;
    private readonly IndexHeadersCache _indexHeadersCache;

    public SearchOrderCache(ILogger<MemoryCacheBase<(IndexHeaders, IReadOnlyList<string>), IReadOnlyList<string>>> logger, ProgressionsCache progressionsCache, IndexHeadersCache indexHeadersCache) 
        : base(logger)
    {
        _progressionsCache = progressionsCache;
        _indexHeadersCache = indexHeadersCache;
    }

    protected override string Key => "SearchOrder";

    protected override async Task<(IndexHeaders, IReadOnlyList<string>)> GetInputModel()
    {
        return (await _indexHeadersCache.Get(), (await _progressionsCache.Get()).Keys.ToList());
    }

    protected override IReadOnlyList<string> GetPresentationModel((IndexHeaders indexHeaders, IReadOnlyList<string> progressionsExternalIds) inputModel)
    {
        return inputModel.indexHeaders.Headers
            .Join(inputModel.progressionsExternalIds, x => x.Key, x => x, (x, y) => x.Value)
            .OrderByDescending(x => x.Rating)
            .Select(x => x.ExternalId)
            .ToList();
    }
}