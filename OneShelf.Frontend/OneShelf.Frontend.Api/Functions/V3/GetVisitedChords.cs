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
        private readonly AuthorizationApiClient _authorizationApiClient;

        public GetVisitedChords(ILoggerFactory loggerFactory, SongsDatabase songsDatabase, AuthorizationApiClient authorizationApiClient)
            : base(loggerFactory, authorizationApiClient)
        {
            _songsDatabase = songsDatabase;
            _authorizationApiClient = authorizationApiClient;
        }

        [Function(ApiUrls.V3GetVisitedChords)]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req, [FromBody] GetVisitedChordsRequest request) => await RunHandler(req, request);

        protected override async Task<GetVisitedChordsResponse> Execute(HttpRequest httpRequest,
            GetVisitedChordsRequest request)
        {
            var source = _songsDatabase.VisitedChords.FromSqlRaw(@"

select [Id]
      ,[ViewedOn]
      ,[UserId]
      ,[Uri]
      ,[SongId]
      ,[ExternalId]
      ,[SearchQuery]
      ,[Transpose]
      ,[Artists]
      ,[Title]
      ,[Source]
from (

	select 
		*, 
		lead(c) over (partition by userid order by viewedon, id) c2, 
		lead(viewedon) over (partition by userid order by viewedon, id) v2
	from (
		select checksum(uri, songid, externalid, SearchQuery, Artists, title, source) c, *
		from visitedchords
	) x

) x
where c <> isnull(c2, 0) or DATEDIFF(minute, v2, viewedon) < -10 

");

            var count = await source.CountAsync(x => x.UserId == request.Identity.Id && x.User.TenantId == TenantId);

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
