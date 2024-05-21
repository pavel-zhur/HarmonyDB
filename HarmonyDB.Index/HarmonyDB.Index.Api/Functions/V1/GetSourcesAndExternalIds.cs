using HarmonyDB.Index.Api.Services;
using HarmonyDB.Sources.Api.Client;
using HarmonyDB.Sources.Api.Model;
using HarmonyDB.Sources.Api.Model.V1.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Common;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.V1;

public class GetSourcesAndExternalIds : AuthorizationFunctionBase<GetSourcesAndExternalIdsRequest, GetSourcesAndExternalIdsResponse>
{
    private readonly CommonExecutions _commonExecutions;

    public GetSourcesAndExternalIds(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, CommonExecutions commonExecutions)
        : base(loggerFactory, authorizationApiClient)
    {
        _commonExecutions = commonExecutions;
    }

    [Function(SourcesApiUrls.V1GetSourcesAndExternalIds)]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBody] GetSourcesAndExternalIdsRequest request)
        => RunHandler(req, request);

    protected override async Task<GetSourcesAndExternalIdsResponse> Execute(HttpRequest httpRequest,
        GetSourcesAndExternalIdsRequest request)
        => await _commonExecutions.GetSourcesAndExternalIds(request);
}