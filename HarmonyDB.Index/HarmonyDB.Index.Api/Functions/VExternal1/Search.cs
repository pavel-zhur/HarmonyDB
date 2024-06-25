using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.Api.Services;
using HarmonyDB.Index.BusinessLogic.Services;
using HarmonyDB.Source.Api.Model.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.VExternal1;

public class Search : ServiceFunctionBase<SearchRequest, SearchResponse>
{
    private readonly ProgressionsCache _progressionsCache;
    private readonly IndexHeadersCache _indexHeadersCache;
    private readonly ProgressionsSearch _progressionsSearch;
    private readonly SearchOrderCache _searchOrderCache;
    private readonly InputParser _inputParser;

    public Search(ILoggerFactory loggerFactory, SecurityContext securityContext, ProgressionsCache progressionsCache, IndexHeadersCache indexHeadersCache, ProgressionsSearch progressionsSearch, SearchOrderCache searchOrderCache, InputParser inputParser)
        : base(loggerFactory, securityContext)
    {
        _progressionsCache = progressionsCache;
        _indexHeadersCache = indexHeadersCache;
        _progressionsSearch = progressionsSearch;
        _searchOrderCache = searchOrderCache;
        _inputParser = inputParser;
    }

    [Function(IndexApiUrls.VExternal1Search)]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] SearchRequest request)
        => RunHandler(request);

    protected override async Task<SearchResponse> Execute(SearchRequest request)
    {
        var progressions = await _progressionsCache.Get();
        var searchOrder = await _searchOrderCache.Get();
        var headers = await _indexHeadersCache.Get();
        var found = _progressionsSearch.Search(searchOrder.Select(x => progressions[x]), _inputParser.Parse(request.Query));

        var results = headers.Headers
            .Select(h => progressions.TryGetValue(h.Key, out var progression) && found.TryGetValue(progression, out var coverage)
                ? (h, coverage).OnceAsNullable()
                : null)
            .Where(x => x.HasValue)
            .Select(x => x.Value)
            .Where(x => x.coverage >= request.MinCoverage && x.h.Value.Rating >= request.MinRating)
            .OrderByDescending<(KeyValuePair<string, IndexHeader> h, float coverage), int>(request.Ordering switch
            {
                SearchRequestOrdering.ByRating => x =>
                    (int)(x.h.Value.Rating * 10 ?? 0) * 10 + (int)(x.coverage * 1000),
                SearchRequestOrdering.ByCoverage => x =>
                    (int)(x.h.Value.Rating * 10 ?? 0) + (int)(x.coverage * 1000) * 10,
                _ => throw new ArgumentOutOfRangeException(),
            })
            .ToList();

        return new()
        {
            Songs = results.Skip((request.PageNumber - 1) * request.SongsPerPage).Take(request.SongsPerPage).Select(x => new SearchResponseSong
            {
                Header = x.h.Value,
                Coverage = x.coverage,
            }).ToList(),
            Total = results.Count,
            TotalPages = results.Count / request.SongsPerPage + (results.Count % request.SongsPerPage == 0 ? 0 : 1),
            CurrentPageNumber = request.PageNumber,
        };
    }
}