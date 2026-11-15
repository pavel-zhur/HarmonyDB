using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Authorization.Api.Model;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Database.Songs;
using OneShelf.Frontend.Api.Model.V3.Api;
using OneShelf.Frontend.Api.Model.V3.Instant;
using OneShelf.Frontend.Api.Model.V3.Api;

namespace OneShelf.Frontend.Api.Functions.V3
{
    public class GetVisitedSearches : AuthorizationFunctionBase<GetVisitedSearchesRequest, GetVisitedSearchesResponse>
    {
        private const int PerPage = 100;

        private readonly SongsDatabase _songsDatabase;

        public GetVisitedSearches(ILoggerFactory loggerFactory, SongsDatabase songsDatabase, AuthorizationApiClient authorizationApiClient, SecurityContext securityContext) 
            : base(loggerFactory, authorizationApiClient, securityContext)
        {
            _songsDatabase = songsDatabase;
        }

        [Function(ApiUrls.V3GetVisitedSearches)]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req, [FromBody] GetVisitedSearchesRequest request) => await RunHandler(req, request);

        protected override async Task<GetVisitedSearchesResponse> Execute(HttpRequest httpRequest,
            GetVisitedSearchesRequest request)
        {
            var source = _songsDatabase.GetVisitedSearchesWithoutRepeats();

            var count = await source.CountAsync(x => x.UserId == request.Identity.Id);

            return new()
            {
                PagesCount = count / PerPage + (count % PerPage != 0 ? 1 : 0),
                VisitedSearches = (await source.Where(x => x.UserId == request.Identity.Id)
                        .OrderByDescending(x => x.SearchedOn).ThenByDescending(x => x.Id).Skip(request.PageIndex * PerPage).Take(PerPage)
                        .ToListAsync())
                    .Select(x => new VisitedSearch
                    {
                        Query = x.Query,
                        HappenedOn = x.SearchedOn,
                    })
                    .ToList(),
            };
        }
    }
}
