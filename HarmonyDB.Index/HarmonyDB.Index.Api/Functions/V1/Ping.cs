using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OneShelf.Common.Api;
using System.Net;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Index.DownstreamApi.Client;
using HarmonyDB.Source.Api.Client;
using HarmonyDB.Source.Api.Model;
using Microsoft.Extensions.Logging;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.V1
{
    public class Ping
    {
        private readonly ILogger _logger;
        private readonly DownstreamApiClient _downstreamApiClient;
        private readonly IndexApiClient _indexApiClient;

        public Ping(ILoggerFactory loggerFactory, DownstreamApiClient downstreamApiClient, IndexApiClient indexApiClient)
        {
            _downstreamApiClient = downstreamApiClient;
            _indexApiClient = indexApiClient;
            _logger = loggerFactory.CreateLogger<Ping>();
        }

        [Function(SourceApiUrls.V1Ping, AuthorizationLevel.Anonymous, "get", "post")]
        public async Task<IActionResult> Run(HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            //await Task.WhenAll(
            //    _downstreamApiClient.PingAll(),
            //    _indexApiClient.PingAll());

            await _downstreamApiClient.PingAll();

            return new ContentResult { Content = "Welcome to Azure Functions!", ContentType = "text/plain; charset=utf-8" };
        }
    }
}
