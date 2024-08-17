using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace OneShelf.OneDragon.Runner.Functions.Functions
{
    public class ManagementFunctions
    {
        private readonly ILogger<ManagementFunctions> _logger;

        public ManagementFunctions(ILogger<ManagementFunctions> logger)
        {
            _logger = logger;
        }

        [Function(nameof(Check))]
        public async Task<HttpResponseData> Check([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync("Welcome to Azure Functions!");

            return response;
        }
    }
}
