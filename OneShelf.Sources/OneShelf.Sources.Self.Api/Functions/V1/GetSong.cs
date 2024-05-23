using HarmonyDB.Source.Api.Model;
using HarmonyDB.Source.Api.Model.V1.Api;
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
    public class GetSong : AuthorizationFunctionBase<GetSongRequest, GetSongResponse>
    {
        private readonly MetadataBuilder _metadataBuilder;
        private readonly CollectivesApiClient _collectivesApiClient;
        private readonly StructureParser _structureParser;

        public GetSong(ILoggerFactory loggerFactory, MetadataBuilder metadataBuilder, CollectivesApiClient collectivesApiClient, StructureParser structureParser, AuthorizationApiClient authorizationApiClient) : base(loggerFactory, authorizationApiClient)
        {
            _metadataBuilder = metadataBuilder;
            _collectivesApiClient = collectivesApiClient;
            _structureParser = structureParser;
        }

        [Function(SourceApiUrls.V1GetSong)]
        public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [Microsoft.Azure.Functions.Worker.Http.FromBody] GetSongRequest request) => RunHandler(req, request);

        protected override async Task<GetSongResponse> Execute(HttpRequest httpRequest, GetSongRequest request)
        {
            if (!_metadataBuilder.IsExternalId(request.ExternalId)) throw new("The song id not found.");

            var collectiveVersion = (await _collectivesApiClient.Get(_metadataBuilder.GetCollectiveId(request.ExternalId))).Version;

            return new()
            {
                Song = _metadataBuilder.GetChords(collectiveVersion,
                    _structureParser.ContentToHtml(collectiveVersion.Collective)),
            };
        }
    }
}
