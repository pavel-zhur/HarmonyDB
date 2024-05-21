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
using OneShelf.Illustrations.Api.Services;

namespace OneShelf.Illustrations.Api.Functions;

public class GetImage : FunctionBase<GetImageRequest>
{
    private readonly IllustrationsCosmosDatabase _illustrationsCosmosDatabase;
    private readonly AutoUploader _autoUploader;

    public GetImage(ILoggerFactory loggerFactory, IllustrationsCosmosDatabase illustrationsCosmosDatabase, AutoUploader autoUploader)
        : base(loggerFactory)
    {
        _illustrationsCosmosDatabase = illustrationsCosmosDatabase;
        _autoUploader = autoUploader;
    }

    [Function(IllustrationsApiUrls.GetImage)]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req, [FromBody] GetImageRequest request) => RunHandler(request);

    protected override async Task<IActionResult> ExecuteSuccessful(GetImageRequest request)
    {
        var illustration = await _illustrationsCosmosDatabase.GetIllustration(request.Id);
        if (illustration == null)
        {
            return new NotFoundResult();
        }

        if (illustration.PublicUrl1024 == null)
        {
            await _autoUploader.Go(illustration.Id);
        }

        return new FileContentResult(illustration.Image, "image/jpeg");
    }
}