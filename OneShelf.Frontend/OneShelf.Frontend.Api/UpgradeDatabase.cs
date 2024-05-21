using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Common.Database.Songs;

namespace OneShelf.Frontend.Api
{
    public class UpgradeDatabase
    {
        private readonly SongsDatabase _songsDatabase;
        private readonly ILogger _logger;

        public UpgradeDatabase(ILoggerFactory loggerFactory, SongsDatabase songsDatabase)
        {
            _songsDatabase = songsDatabase;
            _logger = loggerFactory.CreateLogger<UpgradeDatabase>();
        }

        [Function("UpgradeDatabase")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            try
            {
                await _songsDatabase.Database.MigrateAsync();
                await response.WriteStringAsync("Done");
            }
            catch (Exception e)
            {
                await response.WriteStringAsync(e.ToString());
            }

            return response;
        }
    }
}
