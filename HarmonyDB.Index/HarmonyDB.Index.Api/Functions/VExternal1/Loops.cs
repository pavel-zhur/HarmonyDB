using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.BusinessLogic.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.VExternal1;

public class Loops : ServiceFunctionBase<LoopsRequest, LoopsResponse>
{
    private readonly LoopsStatisticsCache _loopsStatisticsCache;

    public Loops(ILoggerFactory loggerFactory, SecurityContext securityContext, LoopsStatisticsCache loopsStatisticsCache)
        : base(loggerFactory, securityContext)
    {
        _loopsStatisticsCache = loopsStatisticsCache;
    }

    [Function(IndexApiUrls.VExternal1Loops)]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] LoopsRequest request)
        => RunHandler(request);

    protected override async Task<LoopsResponse> Execute(LoopsRequest request)
    {
        IEnumerable<LoopStatistics> results = await _loopsStatisticsCache.Get();

        var loops = (request.Ordering switch
        {
            LoopsRequestOrdering.LengthAscSongsDesc => results.OrderBy(x => x.Length).ThenByDescending(x => x.TotalSongs),
            LoopsRequestOrdering.LengthDescSongsDesc => results.OrderByDescending(x => x.Length).ThenByDescending(x => x.TotalSongs),
            LoopsRequestOrdering.LengthAscSuccessionsDesc => results.OrderBy(x => x.Length).ThenByDescending(x => x.TotalSuccessions),
            LoopsRequestOrdering.LengthDescSuccessionsDesc => results.OrderByDescending(x => x.Length).ThenByDescending(x => x.TotalSuccessions),
            LoopsRequestOrdering.LengthAscOccurrencesDesc => results.OrderBy(x => x.Length).ThenByDescending(x => x.TotalOccurrences),
            LoopsRequestOrdering.LengthDescOccurrencesDesc => results.OrderByDescending(x => x.Length).ThenByDescending(x => x.TotalOccurrences),
            LoopsRequestOrdering.SongsDesc => results.OrderByDescending(x => x.TotalSongs),
            LoopsRequestOrdering.SuccessionsDesc => results.OrderByDescending(x => x.TotalSuccessions),
            LoopsRequestOrdering.OccurrencesDesc => results.OrderByDescending(x => x.TotalOccurrences),
            _ => throw new ArgumentOutOfRangeException()
        })
            .Where(x => x.Length >= request.MinLength)
            .Where(x => x.Length <= (request.MaxLength ?? int.MaxValue))
            .Where(x => x.TotalSongs >= request.MinTotalSongs)
            .Where(x => x.TotalOccurrences >= request.MinTotalOccurrences)
            .Where(x => x.TotalSuccessions >= request.MinTotalSuccessions)
            .ToList();

        return new()
        {
            Total = loops.Count,
            TotalPages = loops.Count / request.LoopsPerPage + (loops.Count % request.LoopsPerPage == 0 ? 0 : 1),
            CurrentPageNumber = request.PageNumber,
            Loops = loops.Skip((request.PageNumber - 1) * request.LoopsPerPage).Take(request.LoopsPerPage).ToList(),
        };
    }
}