using OneShelf.Common.Api;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        [Function("UpgradeDatabase", AuthorizationLevel.Anonymous, "get", "post")]
        public async Task<IActionResult> Run(HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string result;
            try
            {
                await _songsDatabase.Database.MigrateAsync();
                result = "Done";
            }
            catch (Exception e)
            {
                result = e.ToString();
            }

            return new ContentResult { Content = result, ContentType = "text/plain; charset=utf-8" };
        }
    }
}
