using System.Net;
using HarmonyDB.Source.Api.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace OneShelf.Sources.Self.Api.Functions.V1
{
    public class Ping
    {
        private readonly ILogger _logger;

        public Ping(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Ping>();
        }

        [Function(SourceApiUrls.V1Ping)]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync("Welcome to Azure Functions!");

            return response;
        }
    }
}
