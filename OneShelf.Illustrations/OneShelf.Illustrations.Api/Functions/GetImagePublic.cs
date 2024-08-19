using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Illustrations.Api.Model;
using OneShelf.Illustrations.Database;

namespace OneShelf.Illustrations.Api.Functions;

public class GetImagePublic
{
    private readonly IllustrationsCosmosDatabase _illustrationsCosmosDatabase;
    private readonly ILogger<GetImagePublic> _logger;

    public GetImagePublic(ILoggerFactory loggerFactory, IllustrationsCosmosDatabase illustrationsCosmosDatabase)
    {
        _logger = loggerFactory.CreateLogger<GetImagePublic>();
        _illustrationsCosmosDatabase = illustrationsCosmosDatabase;
    }

    [Function(IllustrationsApiUrls.GetImagePublic)]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req, Guid id)
    {
        var illustration = await _illustrationsCosmosDatabase.GetIllustration(id);
        if (illustration == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);

        response.Headers.Add("Content-Type", "image/jpeg");

        await response.WriteBytesAsync(illustration.Image);
        return response;
    }
}