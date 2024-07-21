using HarmonyDB.Index.DownstreamApi.Client;
using HarmonyDB.Source.Api.Client;
using HarmonyDB.Source.Api.Model;
using HarmonyDB.Source.Api.Model.V1;
using HarmonyDB.Source.Api.Model.V1.Api;
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
        private readonly DownstreamApiClient _downstreamApiClient;

        public GetSongs(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, DownstreamApiClient downstreamApiClient, SecurityContext securityContext) 
            : base(loggerFactory, authorizationApiClient, securityContext, respectServiceCode: true)
        {
            _downstreamApiClient = downstreamApiClient;
        }

        [Function(SourceApiUrls.V1GetSongs)]
        public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBody] GetSongsRequest request)
            => RunHandler(req, request);

        protected override async Task<GetSongsResponse> Execute(HttpRequest httpRequest, GetSongsRequest request)
        {
            async Task<List<Chords>> Process(int sourceIndex, IReadOnlyCollection<string> externalIds) 
                => (await _downstreamApiClient.V1GetSongs(request.Identity, sourceIndex, externalIds.ToList()))
                    .Songs
                    .Where(x => externalIds.Contains(x.Key) && x.Key == x.Value.ExternalId && sourceIndex == _downstreamApiClient.GetDownstreamSourceIndexBySourceKey(x.Value.Source))
                    .Select(x =>
                    {
                        x.Value.Source = _downstreamApiClient.GetSourceTitle(x.Value.Source);
                        return x.Value;
                    })
                    .ToList();

            var results = await Task.WhenAll(request.ExternalIds
                .GroupBy(_downstreamApiClient.GetDownstreamSourceIndexByExternalId)
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
