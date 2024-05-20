using System.Net;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OneShelf.Authorization.Api.Client;
using OneShelf.Authorization.Api.Model;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Compression;
using OneShelf.Frontend.Api.AuthorizationQuickCheck;
using OneShelf.Frontend.Api.Model.V3.Api;
using OneShelf.Frontend.Api.Tools;
using OneShelf.Frontend.Database.Cosmos;
using OneShelf.Frontend.Database.Cosmos.Models;

namespace OneShelf.Frontend.Api.Functions.V3;

public class PreviewPdf : FunctionBase<PreviewPdfRequest>
{
    private readonly FrontendCosmosDatabase _frontendCosmosDatabase;
    private readonly AuthorizationQuickChecker _authorizationQuickChecker;

    public PreviewPdf(ILoggerFactory loggerFactory, FrontendCosmosDatabase frontendCosmosDatabase, AuthorizationQuickChecker authorizationQuickChecker)
        : base(loggerFactory)
    {
        _frontendCosmosDatabase = frontendCosmosDatabase;
        _authorizationQuickChecker = authorizationQuickChecker;
    }

    [Function(ApiUrls.V3PreviewPdf)]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req, [FromQuery] string x)
    {
        return RunHandler(JsonConvert.DeserializeObject<PreviewPdfRequest>(x)!);
    }

    protected override async Task<IActionResult> ExecuteSuccessful(PreviewPdfRequest request)
    {
        if (DateTime.Now > new DateTime(request.Expiration)) return new UnauthorizedObjectResult("Expired");

        var hash = await _authorizationQuickChecker.Sign(request.UserId, System.Text.Json.JsonSerializer.Serialize(request.File), request.Expiration);
        var isSuccess = request.Hash == hash;

        if (!isSuccess) return new UnauthorizedObjectResult("BadHash");

        return new FileContentResult(await CompressionTools.DecompressToBytes((await _frontendCosmosDatabase.GetPdfs(
            new()
            {
                Pdf.GetId((request.File.ExternalId, request.File.ToPdfConfiguration(), GetPdfs.Version))
            })).Single().Value.Data), "application/pdf");
    }
}