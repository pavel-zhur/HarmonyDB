using OneShelf.Common.Api;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OneShelf.Illustrations.Api.Model;
using OneShelf.Illustrations.Database;

namespace OneShelf.Illustrations.Api.Functions;

public class GetImagePublic
{
    private readonly IllustrationsDatabase _illustrationsDatabase;
    private readonly ILogger<GetImagePublic> _logger;

    public GetImagePublic(ILoggerFactory loggerFactory, IllustrationsDatabase illustrationsDatabase)
    {
        _logger = loggerFactory.CreateLogger<GetImagePublic>();
        _illustrationsDatabase = illustrationsDatabase;
    }

    [Function(IllustrationsApiUrls.GetImagePublic, AuthorizationLevel.Function, "get")]
    public async Task<IActionResult> Run(HttpRequest req, Guid id)
    {
        var illustration = await _illustrationsDatabase.GetIllustration(id);
        if (illustration == null)
        {
            return new NotFoundResult();
        }

        return new FileContentResult(illustration.Image, "image/jpeg");
    }
}