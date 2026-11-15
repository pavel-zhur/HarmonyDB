using System.Drawing.Imaging;
using System.Drawing;
using System.Net;
using Azure.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Illustrations.Api.Model;
using OneShelf.Illustrations.Database;
using System.Security.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OneShelf.Common.Api;

namespace OneShelf.Illustrations.Api.Functions;

public class GetImage : FunctionBase<GetImageRequest>
{
    private readonly IllustrationsDatabase _illustrationsDatabase;

    public GetImage(ILoggerFactory loggerFactory, IllustrationsDatabase illustrationsDatabase)
        : base(loggerFactory)
    {
        _illustrationsDatabase = illustrationsDatabase;
    }

    [Function(IllustrationsApiUrls.GetImage)]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req, [FromBody] GetImageRequest request) => RunHandler(request);

    protected override async Task<IActionResult> ExecuteSuccessful(GetImageRequest request)
    {
        var illustration = await _illustrationsDatabase.GetIllustration(request.Id);
        if (illustration == null)
        {
            return new NotFoundResult();
        }

        return new FileContentResult(illustration.Image, "image/jpeg");
    }
}