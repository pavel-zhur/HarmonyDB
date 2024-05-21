using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Common.Api;
using OneShelf.Frontend.Api.Model.V3.Api;
using OneShelf.Sources.Self.Api.Client;

namespace OneShelf.Frontend.Api.Functions.V3;

public class FormatPreview : FunctionBase<FormatPreviewRequest, FormatPreviewResponse>
{
    private readonly SelfApiClient _selfApiClient;

    public FormatPreview(ILoggerFactory loggerFactory, SelfApiClient selfApiClient) 
        : base(loggerFactory)
    {
        _selfApiClient = selfApiClient;
    }

    [Function(ApiUrls.V3FormatPreview)]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBody] FormatPreviewRequest request) => await RunHandler(request);

    protected override async Task<FormatPreviewResponse> Execute(FormatPreviewRequest request) =>
        new()
        {
            Output = await _selfApiClient.V1FormatPreview(new()
            {
                Content = request.Content,
            }),
        };
}