using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Common.Database.Songs;

namespace OneShelf.Authorization.Api.Functions
{
    public class Ping
    {
        private readonly SongsDatabase _songsDatabase;
        private readonly ILogger _logger;

        public Ping(ILoggerFactory loggerFactory, SongsDatabase songsDatabase)
        {
            _songsDatabase = songsDatabase;
            _logger = loggerFactory.CreateLogger<Ping>();
        }

        [Function("Ping")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            await _songsDatabase.Users.CountAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync("Welcome to Azure Functions!");

            return response;
        }
    }
}
