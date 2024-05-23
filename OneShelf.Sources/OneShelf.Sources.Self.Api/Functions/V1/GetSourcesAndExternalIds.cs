using HarmonyDB.Source.Api.Model;
using HarmonyDB.Source.Api.Model.V1;
using HarmonyDB.Source.Api.Model.V1.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Authorization.Api.Client;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Sources.Self.Api.Models;
using OneShelf.Sources.Self.Api.Services;

namespace OneShelf.Sources.Self.Api.Functions.V1;

public class GetSourcesAndExternalIds : AuthorizationFunctionBase<GetSourcesAndExternalIdsRequest, GetSourcesAndExternalIdsResponse>
{
    private readonly MetadataBuilder _metadataBuilder;
    private readonly SelfApiOptions _options;

    public GetSourcesAndExternalIds(ILoggerFactory loggerFactory, MetadataBuilder metadataBuilder, IOptions<SelfApiOptions> options, AuthorizationApiClient authorizationApiClient) : base(loggerFactory, authorizationApiClient)
    {
        _metadataBuilder = metadataBuilder;
        _options = options.Value;
    }

    [Function(SourceApiUrls.V1GetSourcesAndExternalIds)]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [Microsoft.Azure.Functions.Worker.Http.FromBody] GetSourcesAndExternalIdsRequest request) => RunHandler(req, request);

    protected override async Task<GetSourcesAndExternalIdsResponse> Execute(HttpRequest httpRequest,
        GetSourcesAndExternalIdsRequest request)
    {
        return new()
        {
            Attributes = request.Uris
                .Distinct()
                .Select(x => (uri: x, externalId: _metadataBuilder.GetExternalId(x)))
                .Where(x => x.externalId != null)
                .Select(x => (x.uri, attributes: new UriAttributes
                {
                    Source = _options.SourceName,
                    ExternalId = x.externalId!,
                }))
                .ToDictionary(x => x.uri.ToString(), x => x.attributes),
        };
    }
}