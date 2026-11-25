using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OneShelf.Common.Api;
using OneShelf.Illustrations.Api.Model;
using OneShelf.Illustrations.Database;

namespace OneShelf.Illustrations.Api.Functions;

public class GetNonEmptyUrls : FunctionBase<GetNonEmptyUrlsRequest, GetNonEmptyUrlsResponse>
{
    private readonly IllustrationsDatabase _illustrationsDatabase;

    public GetNonEmptyUrls(ILoggerFactory loggerFactory, IllustrationsDatabase illustrationsDatabase)
        : base(loggerFactory)
    {
        _illustrationsDatabase = illustrationsDatabase;
    }

    [Function(IllustrationsApiUrls.GetNonEmptyUrls, AuthorizationLevel.Function, "post", InternalScope = IllustrationsModule.InternalScope)]
    public Task<IActionResult> Run(HttpRequest req, [FromBody] GetNonEmptyUrlsRequest request) => RunHandler(request);

    protected override async Task<GetNonEmptyUrlsResponse> Execute(GetNonEmptyUrlsRequest getPdfsRequest)
    {
        return new()
        {
            Urls = await _illustrationsDatabase.GetNonEmptyUrls(),
        };
    }
}