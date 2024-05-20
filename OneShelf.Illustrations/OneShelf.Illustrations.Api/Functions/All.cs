using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Illustrations.Api.Model;
using OneShelf.Illustrations.Database;
using OneShelf.Illustrations.Database.Models;
using System.Drawing.Imaging;
using System.Text.Json;
using Grpc.Net.Client.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OneShelf.Common;
using OneShelf.Common.Api;
using OneShelf.Illustrations.Api.Constants;
using OneShelf.Illustrations.Api.Services;

namespace OneShelf.Illustrations.Api.Functions
{
    public class All : FunctionBase<AllRequest, AllResponse>
    {
        private readonly AllReader _allReader;

        public All(ILoggerFactory loggerFactory, AllReader allReader)
           : base(loggerFactory)
        {
            _allReader = allReader;
        }

        [Function(IllustrationsApiUrls.All)]
        public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req, [FromBody] AllRequest request) => RunHandler(request);

        protected override async Task<AllResponse> Execute(AllRequest allRequest)
        {
            return await _allReader.Read();
        }
    }
}
