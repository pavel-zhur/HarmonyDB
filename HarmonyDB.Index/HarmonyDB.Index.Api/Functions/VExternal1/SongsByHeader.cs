using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.Api.Models;
using HarmonyDB.Index.BusinessLogic.Services;
using HarmonyDB.Index.DownstreamApi.Client;
using HarmonyDB.Source.Api.Model.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.VExternal1;

public class SongsByHeader : ServiceFunctionBase<SongsByHeaderRequest, SongsByHeaderResponse>
{
    private readonly DownstreamApiClient _downstreamApiClient;
    private readonly IndexApiOptions _options;
    private readonly IndexApiClient _indexApiClient;
    private readonly FullTextSearchCache _fullTextSearchCache;

    public SongsByHeader(ILoggerFactory loggerFactory, SecurityContext securityContext, DownstreamApiClient downstreamApiClient, ConcurrencyLimiter concurrencyLimiter, IOptions<IndexApiOptions> options, IndexApiClient indexApiClient, FullTextSearchCache fullTextSearchCache)
        : base(loggerFactory, securityContext, concurrencyLimiter, options.Value.RedirectCachesToIndex)
    {
        _downstreamApiClient = downstreamApiClient;
        _indexApiClient = indexApiClient;
        _fullTextSearchCache = fullTextSearchCache;
        _options = options.Value;
    }

    [Function(IndexApiUrls.VExternal1SongsByHeader)]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] SongsByHeaderRequest request)
        => RunHandler(request);

    protected override async Task<SongsByHeaderResponse> Execute(SongsByHeaderRequest request)
    {
        if (_options.RedirectCachesToIndex)
        {
            return await _indexApiClient.SongsByHeader(request);
        }

        var cache = await _fullTextSearchCache.Get();
        var found = cache.Find(request.Query);

        var results = found
            .Where(x => x.Rating >= request.MinRating)
            .ToList();

        return new()
        {
            Songs = results.Skip((request.PageNumber - 1) * request.SongsPerPage).Take(request.SongsPerPage).ToList(),
            Total = results.Count,
            TotalPages = results.Count / request.SongsPerPage + (results.Count % request.SongsPerPage == 0 ? 0 : 1),
            CurrentPageNumber = request.PageNumber,
        };
    }
}