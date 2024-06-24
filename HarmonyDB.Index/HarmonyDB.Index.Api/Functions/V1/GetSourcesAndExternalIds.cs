using HarmonyDB.Index.Api.Services;
using HarmonyDB.Source.Api.Model;
using HarmonyDB.Source.Api.Model.V1.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.V1;

public class GetSourcesAndExternalIds : AuthorizationFunctionBase<GetSourcesAndExternalIdsRequest, GetSourcesAndExternalIdsResponse>
{
    private readonly CommonExecutions _commonExecutions;

    public GetSourcesAndExternalIds(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, CommonExecutions commonExecutions, SecurityContext securityContext)
        : base(loggerFactory, authorizationApiClient, securityContext)
    {
        _commonExecutions = commonExecutions;
    }

    [Function(SourceApiUrls.V1GetSourcesAndExternalIds)]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBody] GetSourcesAndExternalIdsRequest request)
        => RunHandler(req, request);

    protected override async Task<GetSourcesAndExternalIdsResponse> Execute(HttpRequest httpRequest,
        GetSourcesAndExternalIdsRequest request)
        => await _commonExecutions.GetSourcesAndExternalIds(request.Uris);
}