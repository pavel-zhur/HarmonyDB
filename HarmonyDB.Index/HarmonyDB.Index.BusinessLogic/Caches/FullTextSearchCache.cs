using HarmonyDB.Common.FullTextSearch;
using HarmonyDB.Index.BusinessLogic.Caches.Bases;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;

namespace HarmonyDB.Index.BusinessLogic.Caches;

public class FullTextSearchCache : MemoryCacheBase<IndexHeaders, FullTextSearch>
{
    private readonly IndexHeadersCache _indexHeadersCache;

    public FullTextSearchCache(ILogger<MemoryCacheBase<IndexHeaders, FullTextSearch>> logger, IndexHeadersCache indexHeadersCache)
        : base(logger)
    {
        _indexHeadersCache = indexHeadersCache;
    }

    protected override string Key => "FullTextSearch";

    protected override async Task<IndexHeaders> GetInputModel() => await _indexHeadersCache.Get();

    protected override FullTextSearch GetPresentationModel(IndexHeaders inputModel)
    {
        return new(inputModel.Headers.Values
            .SelectMany(h =>
                (h.Artists ?? Enumerable.Empty<string>())
                    .Append(h.Title)
                    .Where(x => x != null)
                    .Select(x => x!.SearchSyntaxRemoveSeparators())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .SelectMany(x => x.ToSearchSyntax()
                        .Split(FullTextSearchExtensions.Separators, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x)))
                    .Select(w => (h, w)))
            .ToLookup(x => x.w, x => x.h));
    }
}