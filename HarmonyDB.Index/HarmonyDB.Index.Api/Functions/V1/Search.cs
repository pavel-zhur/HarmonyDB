using HarmonyDB.Source.Api.Client;
using HarmonyDB.Source.Api.Model;
using HarmonyDB.Source.Api.Model.V1.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Common;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.V1
{
    public class Search : AuthorizationFunctionBase<SearchRequest, SearchResponse>
    {
        private readonly SourcesApiClient.SourcesApiClient _sourcesApiClient;

        public Search(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, SourcesApiClient.SourcesApiClient sourcesApiClient) : base(loggerFactory, authorizationApiClient)
        {
            _sourcesApiClient = sourcesApiClient;
        }

        [Function(SourceApiUrls.V1Search)]
        public Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBody] SearchRequest request)
            => RunHandler(req, request);

        protected override async Task<SearchResponse> Execute(HttpRequest httpRequest, SearchRequest request)
        {
            var query = request.Query;
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentOutOfRangeException(nameof(query));
            }

            var all = await Task.WhenAll(_sourcesApiClient.SearchableIndices.Select(i => _sourcesApiClient.V1Search(i,
                new()
                {
                    Identity = request.Identity,
                    Query = query,
                })));

            return new()
            {
                Headers = all
                    .WithIndices()
                    .SelectMany(x => x.x.Headers.Where(h => _sourcesApiClient.SourceIndices[h.Source] == x.i))
                    .ToList(),
            };
        }
    }
}
