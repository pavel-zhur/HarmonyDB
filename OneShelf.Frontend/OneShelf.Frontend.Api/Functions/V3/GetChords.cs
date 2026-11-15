using OneShelf.Common.Api;
using System.Net;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Source.Api.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OneShelf.Frontend.Api.Model.V3.Api;

namespace OneShelf.Frontend.Api.Functions.V3
{
    public class GetChords
    {
        private readonly ILogger _logger;
        private readonly SourceApiClient _sourceApiClient;

        public GetChords(ILoggerFactory loggerFactory, SourceApiClient sourceApiClient)
        {
            _sourceApiClient = sourceApiClient;
            _logger = loggerFactory.CreateLogger<GetChords>();
        }

        [Function(ApiUrls.V3GetChords, AuthorizationLevel.Anonymous, "post")]
        public async Task<IActionResult> Run(HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            using var reader = new StreamReader(req.Body);
            var request = await reader.ReadToEndAsync();

            var result = await _sourceApiClient.V1GetSongsDirect(request);

            await req.HttpContext.Response.WriteAsync(result);

            return new EmptyResult();
        }
    }
}
