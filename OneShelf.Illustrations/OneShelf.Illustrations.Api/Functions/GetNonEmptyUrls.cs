using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Common.Api;
using OneShelf.Illustrations.Api.Model;
using OneShelf.Illustrations.Database;

namespace OneShelf.Illustrations.Api.Functions;

public class GetNonEmptyUrls : FunctionBase<GetNonEmptyUrlsRequest, GetNonEmptyUrlsResponse>
{
    private readonly IllustrationsCosmosDatabase _illustrationsCosmosDatabase;

    public GetNonEmptyUrls(ILoggerFactory loggerFactory, IllustrationsCosmosDatabase illustrationsCosmosDatabase)
        : base(loggerFactory)
    {
        _illustrationsCosmosDatabase = illustrationsCosmosDatabase;
    }

    [Function(IllustrationsApiUrls.GetNonEmptyUrls)]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] GetNonEmptyUrlsRequest request) => RunHandler(request);

    protected override async Task<GetNonEmptyUrlsResponse> Execute(GetNonEmptyUrlsRequest getPdfsRequest)
    {
        return new()
        {
            Urls = await _illustrationsCosmosDatabase.GetNonEmptyUrls(),
        };
    }
}