using System.Drawing.Imaging;
using System.Drawing;
using System.Net;
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

    [Function(IllustrationsApiUrls.GetImage, AuthorizationLevel.Function, "get", "post", InternalScope = IllustrationsModule.InternalScope)]
    public Task<IActionResult> Run(HttpRequest req, [FromBody] GetImageRequest request) => RunHandler(request);

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