using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OneShelf.Common.Api;
using System.Net;
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

        [Function("Ping", AuthorizationLevel.Anonymous, "get", "post")]
        public async Task<IActionResult> Run(HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            await _songsDatabase.Users.CountAsync();

            return new ContentResult { Content = "Welcome to Azure Functions!", ContentType = "text/plain; charset=utf-8" };
        }
    }
}
