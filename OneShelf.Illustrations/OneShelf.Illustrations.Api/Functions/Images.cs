using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Net.Http.Headers;
using OneShelf.Illustrations.Api.Model;
using OneShelf.Illustrations.Database;

namespace OneShelf.Illustrations.Api.Functions;

/// <summary>
/// A copy of an illustration, for the browsers to show: the public address of the picture, needing no key.
/// A copy never changes, so it is cached for a year.
/// </summary>
public class Images(IllustrationsDatabase illustrationsDatabase)
{
    [Function(IllustrationsApiUrls.Images)]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = $"{IllustrationsApiUrls.Images}/{{id:guid}}/{{size:int}}")] HttpRequest req,
        Guid id,
        int size)
    {
        var image = await illustrationsDatabase.GetIllustrationCopy(id, size);
        if (image == null) return new NotFoundResult();

        req.HttpContext.Response.Headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
        return new FileContentResult(image, "image/jpeg");
    }
}
