using System.Net;
using HarmonyDB.Source.Api.Client;
using HarmonyDB.Source.Api.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace HarmonyDB.Index.Api.Functions.V1
{
    public class Ping
    {
        private readonly ILogger _logger;
        private readonly SourcesApiClient _sourcesApiClient;

        public Ping(ILoggerFactory loggerFactory, SourcesApiClient sourcesApiClient)
        {
            _sourcesApiClient = sourcesApiClient;
            _logger = loggerFactory.CreateLogger<Ping>();
        }

        [Function(SourceApiUrls.V1Ping)]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            await Task.WhenAll(Enumerable.Range(0, _sourcesApiClient.SourcesCount).Select(x => _sourcesApiClient.V1Ping(x)));

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync("Welcome to Azure Functions!");

            return response;
        }
    }
}
