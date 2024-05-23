using HarmonyDB.Index.BusinessLogic.Services;
using HarmonyDB.Source.Api.Client;
using HarmonyDB.Source.Api.Model;
using HarmonyDB.Source.Api.Model.V1.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.V1;

public class GetSearchHeader : AuthorizationFunctionBase<GetSearchHeaderRequest, GetSearchHeaderResponse>
{
    private readonly SourcesApiClient.SourcesApiClient _sourcesApiClient;
    private readonly SourceResolver _sourceResolver;

    public GetSearchHeader(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, SourcesApiClient.SourcesApiClient sourcesApiClient, SourceResolver sourceResolver) : base(loggerFactory, authorizationApiClient)
    {
        _sourcesApiClient = sourcesApiClient;
        _sourceResolver = sourceResolver;
    }

    [Function(SourceApiUrls.V1GetSearchHeader)]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBody] GetSearchHeaderRequest request)
        => RunHandler(req, request);

    protected override async Task<GetSearchHeaderResponse> Execute(HttpRequest httpRequest,
        GetSearchHeaderRequest request)
    {
        return new()
        {
            SearchHeader = await _sourcesApiClient.V1GetSearchHeader(request.Identity, _sourcesApiClient.SourceIndices[_sourceResolver.GetSource(request.ExternalId)], request.ExternalId),
        };
    }
}