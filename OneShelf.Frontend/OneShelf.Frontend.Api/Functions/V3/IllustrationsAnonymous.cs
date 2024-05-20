using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Frontend.Api.Model.V3.Api;
using OneShelf.Frontend.Api.Services;

namespace OneShelf.Frontend.Api.Functions.V3;

public class IllustrationsAnonymous : FunctionBase<object, IllustrationsResponse>
{
    private readonly IllustrationsReader _illustrationsReader;
    private readonly SongsDatabase _songsDatabase;

    public IllustrationsAnonymous(ILoggerFactory loggerFactory, IllustrationsReader illustrationsReader, SongsDatabase songsDatabase)
        : base(loggerFactory)
    {
        _illustrationsReader = illustrationsReader;
        _songsDatabase = songsDatabase;
    }

    [Function(ApiUrls.V3IllustrationsAnonymous)]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        => await RunHandler(new());

    protected override async Task<IllustrationsResponse> Execute(object unused)
    {
        return await _illustrationsReader.Go(null, IllustrationsReader.Mode.CollapseSongsIncludeTitles);
    }
}