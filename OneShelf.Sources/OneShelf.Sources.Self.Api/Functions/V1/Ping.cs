using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OneShelf.Common.Api;
using System.Net;
using HarmonyDB.Source.Api.Model;
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

        [Function(SourceApiUrls.V1Ping, AuthorizationLevel.Anonymous, "get", "post")]
        public async Task<IActionResult> Run(HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            return new ContentResult { Content = "Welcome to Azure Functions!", ContentType = "text/plain; charset=utf-8" };
        }
    }
}
