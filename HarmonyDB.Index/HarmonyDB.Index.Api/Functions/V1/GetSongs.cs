using HarmonyDB.Index.BusinessLogic.Services;
using HarmonyDB.Sources.Api.Client;
using HarmonyDB.Sources.Api.Model;
using HarmonyDB.Sources.Api.Model.V1;
using HarmonyDB.Sources.Api.Model.V1.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.V1
{
    public class GetSongs : AuthorizationFunctionBase<GetSongsRequest, GetSongsResponse>
    {
        private readonly SourcesApiClient _sourcesApiClient;
        private readonly SourceResolver _sourceResolver;

        public GetSongs(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, SourcesApiClient sourcesApiClient, SourceResolver sourceResolver) 
            : base(loggerFactory, authorizationApiClient)
        {
            _sourcesApiClient = sourcesApiClient;
            _sourceResolver = sourceResolver;
        }

        [Function(SourcesApiUrls.V1GetSongs)]
        public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBody] GetSongsRequest request)
            => RunHandler(req, request);

        protected override async Task<GetSongsResponse> Execute(HttpRequest httpRequest, GetSongsRequest request)
        {
            async Task<List<Chords>> Process(int sourceIndex, IReadOnlyCollection<string> externalIds) 
                => (await _sourcesApiClient.V1GetSongs(request.Identity, sourceIndex, externalIds.ToList()))
                    .Songs
                    .Where(x => externalIds.Contains(x.Key) && x.Key == x.Value.ExternalId && sourceIndex == _sourcesApiClient.SourceIndices[x.Value.Source])
                    .Select(x => x.Value)
                    .ToList();

            var results = await Task.WhenAll(request.ExternalIds
                .GroupBy(x => _sourcesApiClient.SourceIndices[_sourceResolver.GetSource(x)])
                .Select(source => Process(source.Key, source.ToHashSet())));

            return new()
            {
                Songs = results
                    .SelectMany(x => x)
                    .GroupBy(x => x.ExternalId)
                    .Select(g => g.Single())
                    .ToDictionary(x => x.ExternalId),
            };
        }
    }
}
