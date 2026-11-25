using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Models.Ids;
using HarmonyDB.Index.Analysis.Tools;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.Api.Model.VExternal1.Tonalities;
using HarmonyDB.Index.Api.Models;
using HarmonyDB.Index.Api.Services;
using HarmonyDB.Index.Api.Tools;
using HarmonyDB.Index.BusinessLogic.Caches;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Common.Api;
using OneShelf.Common.Api.Client;
using OneShelf.Common.Api.Common;
using OneShelf.Common.Api.WithAuthorization;
using Loop = HarmonyDB.Index.Api.Model.VExternal1.Tonalities.Loop;

namespace HarmonyDB.Index.Api.Functions.VExternal1;

public class TonalitiesSong : ServiceFunctionBase<SongRequest, SongResponse>
{
    private readonly IndexApiOptions _options;
    private readonly IndexApiClient _indexApiClient;
    private readonly StructuresCache _structuresCache;
    private readonly TonalitiesCache _tonalitiesCache;
    private readonly IndexHeadersCache _indexHeadersCache;
    private readonly CommonExecutions _commonExecutions;
    private readonly IndexIds _ids;

    public TonalitiesSong(ILoggerFactory loggerFactory, SecurityContext securityContext, ConcurrencyLimiter concurrencyLimiter, IOptions<IndexApiOptions> options, IndexApiClient indexApiClient, StructuresCache structuresCache, TonalitiesCache tonalitiesCache, IndexHeadersCache indexHeadersCache, CommonExecutions commonExecutions, IndexIds ids)
        : base(loggerFactory, securityContext, options.Value.LimitConcurrency ? concurrencyLimiter : null)
    {
        _indexApiClient = indexApiClient;
        _structuresCache = structuresCache;
        _tonalitiesCache = tonalitiesCache;
        _indexHeadersCache = indexHeadersCache;
        _commonExecutions = commonExecutions;
        _ids = ids;
        _options = options.Value;
    }

    [Function(IndexApiUrls.VExternal1TonalitiesSong, AuthorizationLevel.Function, "post", InternalScope = IndexInternalScopes.Index)]
    public Task<IActionResult> Run(HttpRequest req, [FromBody] SongRequest request)
        => RunHandler(request);

    protected override async Task<SongResponse> Execute(SongRequest request)
    {
        if (_options.RedirectCachesToIndex)
        {
            return await _indexApiClient.TonalitiesSong(request);
        }

        var tonalities = await _tonalitiesCache.Get();
        var structures = await _structuresCache.Get();
        var headers = await _indexHeadersCache.Get();

        var song = FindSong(_ids, request.ExternalId, structures.Songs, tonalities.Songs.Positions, headers.Songs);
        var structureSong = structures.GetSong(song);
        var songTonality = tonalities.Songs.Positions.TryGetPosition(song, out var value) ? value : throw new CacheItemNotFoundException();
        var header = headers.Get(headers.Songs.GetPosition(song));
        header = _commonExecutions.PrepareForOutput(header) ?? throw new("The song header is not available.");

        return new()
        {
            Song = new(
                tonalities.Songs.GetId(songTonality),
                structureSong.TotalLoops,
                tonalities.Songs.GetProbabilities(songTonality),
                tonalities.Songs.GetScore(songTonality).TonicScore,
                tonalities.Songs.GetScore(songTonality).ScaleScore,
                header,
                tonalities.Songs.GetKnownTonality(songTonality)?.FromEm().ToIndex()),

            Links = structures.GetLinksBySong(song).Select(x => structures.ToStructureLink(x).ToModel()).ToList(),

            Loops = structures.GetLinksBySong(song)
                .Select(x => x.Loop)
                .Distinct()
                .Select(loop =>
                {
                    var stats = structures.GetLoop(loop);
                    var tone = tonalities.Loops.Positions.GetPosition(loop);

                    return new Loop(
                        stats.Normalized,
                        stats.Length,
                        stats.TotalOccurrences,
                        stats.TotalSuccessions,
                        stats.AverageCoverage,
                        stats.TotalSongs,
                        tonalities.Loops.GetProbabilities(tone),
                        tonalities.Loops.GetScore(tone).ScaleScore,
                        tonalities.Loops.GetScore(tone).TonicScore
                    );
                })
                .ToList(),
        };
    }

    /// <summary>
    /// The song's number in the caches. A song the index has not analysed — an id it does not know, or one missing from
    /// any of the collections the answer reads — is answered as not found, not as a failure.
    /// </summary>
    /// <exception cref="CacheItemNotFoundException">The index has not analysed the song.</exception>
    public static int FindSong(IndexIds ids, string externalId, params IdPositions[] analysedIn)
        => ids.Songs.TryGetNumber(externalId, out var song) && analysedIn.All(x => x.Contains(song))
            ? song
            : throw new CacheItemNotFoundException();
}