using System.Net;
using HarmonyDB.Index.Api.Client;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Frontend.Api.Model.V3.Api;

namespace OneShelf.Frontend.Api.Functions.V3
{
    public class GetChords
    {
        private readonly ILogger _logger;
        private readonly IndexApiClient _indexApiClient;

        public GetChords(ILoggerFactory loggerFactory, IndexApiClient indexApiClient)
        {
            _indexApiClient = indexApiClient;
            _logger = loggerFactory.CreateLogger<GetChords>();
        }

        [Function(ApiUrls.V3GetChords)]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var request = await req.ReadAsStringAsync();

            var result = await _indexApiClient.V1GetSongsDirect(request);

            var response = req.CreateResponse(HttpStatusCode.OK);

            await response.WriteStringAsync(result);

            return response;
        }
    }
}
