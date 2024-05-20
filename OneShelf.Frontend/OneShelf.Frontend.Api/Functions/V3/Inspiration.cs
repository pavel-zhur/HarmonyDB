using System.Net;
using HarmonyDB.Index.Api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Authorization.Api.Model;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Frontend.Api.Model.V3.Api;
using OneShelf.Pdfs.Generation.Inspiration.Models;
using OneShelf.Pdfs.Generation.Inspiration.Services;

namespace OneShelf.Frontend.Api.Functions.V3;

public class Inspiration : AuthorizationFunctionBase<InspirationRequest>
{
    private readonly InspirationGeneration _inspirationGeneration;

    public Inspiration(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, InspirationGeneration inspirationGeneration)
        : base(loggerFactory, authorizationApiClient)
    {
        _inspirationGeneration = inspirationGeneration;
    }

    [Function(ApiUrls.V3Inspiration)]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBody] InspirationRequest request) => await RunHandler(req, request);

    protected override async Task<IActionResult> ExecuteSuccessful(HttpRequest httpRequest, InspirationRequest request)
    {
        var pdf = await _inspirationGeneration.Inspiration(TenantId, InspirationDataOrdering.ByArtist, false, true, true, false);
        return new FileContentResult(pdf, "application/pdf");
    }
}