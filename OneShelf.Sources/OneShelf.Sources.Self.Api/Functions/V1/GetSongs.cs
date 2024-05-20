using HarmonyDB.Sources.Api.Model;
using HarmonyDB.Sources.Api.Model.V1;
using HarmonyDB.Sources.Api.Model.V1.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Authorization.Api.Client;
using OneShelf.Collectives.Api.Client;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Sources.Self.Api.Models;
using OneShelf.Sources.Self.Api.Services;

namespace OneShelf.Sources.Self.Api.Functions.V1
{
    public class GetSongs : AuthorizationFunctionBase<GetSongsRequest, GetSongsResponse>
    {
        private readonly MetadataBuilder _metadataBuilder;
        private readonly CollectivesApiClient _collectivesApiClient;
        private readonly StructureParser _structureParser;

        public GetSongs(ILoggerFactory loggerFactory, MetadataBuilder metadataBuilder, CollectivesApiClient collectivesApiClient, StructureParser structureParser, AuthorizationApiClient authorizationApiClient) : base(loggerFactory, authorizationApiClient)
        {
            _metadataBuilder = metadataBuilder;
            _collectivesApiClient = collectivesApiClient;
            _structureParser = structureParser;
        }

        [Function(SourcesApiUrls.V1GetSongs)]
        public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [Microsoft.Azure.Functions.Worker.Http.FromBody] GetSongsRequest request) => RunHandler(req, request);

        protected override async Task<GetSongsResponse> Execute(HttpRequest httpRequest, GetSongsRequest request)
        {
            var chords = new Dictionary<string, Chords>();

            foreach (var externalId in request.ExternalIds)
            {
                if (!_metadataBuilder.IsExternalId(externalId)) continue;

                var collectiveVersion = (await _collectivesApiClient.Get(_metadataBuilder.GetCollectiveId(externalId))).Version;

                chords[externalId] = _metadataBuilder.GetChords(collectiveVersion, _structureParser.ContentToHtml(collectiveVersion.Collective));
            }

            return new()
            {
                Songs = chords,
            };
        }
    }
}
