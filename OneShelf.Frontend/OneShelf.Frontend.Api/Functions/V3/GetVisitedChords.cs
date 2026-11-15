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

namespace OneShelf.Frontend.Api.Functions.V3
{
    public class GetVisitedChords : AuthorizationFunctionBase<GetVisitedChordsRequest, GetVisitedChordsResponse>
    {
        private const int PerPage = 100;

        private readonly SongsDatabase _songsDatabase;

        public GetVisitedChords(ILoggerFactory loggerFactory, SongsDatabase songsDatabase, AuthorizationApiClient authorizationApiClient, SecurityContext securityContext)
            : base(loggerFactory, authorizationApiClient, securityContext)
        {
            _songsDatabase = songsDatabase;
        }

        [Function(ApiUrls.V3GetVisitedChords)]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req, [FromBody] GetVisitedChordsRequest request) => await RunHandler(req, request);

        protected override async Task<GetVisitedChordsResponse> Execute(HttpRequest httpRequest,
            GetVisitedChordsRequest request)
        {
            var source = _songsDatabase.GetVisitedChordsWithoutRepeats();

            var count = await source.CountAsync(x => x.UserId == request.Identity.Id && x.User.TenantId == SecurityContext.TenantId);

            return new()
            {
                PagesCount = count / PerPage + (count % PerPage != 0 ? 1 : 0),
                VisitedChords = (await source.Where(x => x.UserId == request.Identity.Id)
                        .OrderByDescending(x => x.ViewedOn).ThenByDescending(x => x.Id).Skip(request.PageIndex * PerPage).Take(PerPage)
                        .ToListAsync())
                    .Select(x => new VisitedChords
                    {
                        HappenedOn = x.ViewedOn,
                        ExternalId = x.ExternalId,
                        Uri = x.Uri,
                        Transpose = x.Transpose,
                        SearchQuery = x.SearchQuery,
                        Artists = x.Artists,
                        Title = x.Title,
                        SongId = x.SongId,
                        Source = x.Source,
                    })
                    .ToList(),
            };
        }
    }
}
