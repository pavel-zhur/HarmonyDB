using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Billing.Api.Model;
using Microsoft.EntityFrameworkCore;
using OneShelf.Common;
using OneShelf.Common.Api;
using OneShelf.Common.Database.Songs;

namespace OneShelf.Billing.Api.Functions
{
    public class All : FunctionBase<AllRequest, AllResponse>
    {
        private readonly SongsDatabase _songsDatabase;

        public All(ILoggerFactory loggerFactory, SongsDatabase songsDatabase)
           : base(loggerFactory)
        {
            _songsDatabase = songsDatabase;
        }

        [Function(BillingApiUrls.All)]
        public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req, [FromBody] AllRequest request)
            => RunHandler(request);

        protected override async Task<AllResponse> Execute(AllRequest allRequest)
        {
            return new()
            {
                Usages = (await _songsDatabase.BillingUsages
                        .SelectSingle(x => allRequest.DomainId.HasValue ? x.Where(x => x.DomainId == allRequest.DomainId) : x)
                        .SelectSingle(x =>
                        {
                            if (!allRequest.Window.HasValue) 
                                return x;

                            var from = DateTime.Now.Add(-allRequest.Window.Value);
                            return x.Where(x => x.CreatedOn >= from);
                        })
                        .ToListAsync())
                    .Select(x => new Usage
                    {
                        Count = x.Count,
                        AdditionalInfo = x.AdditionalInfo,
                        DomainId = x.DomainId,
                        CreatedOn = x.CreatedOn,
                        InputTokens = x.InputTokens,
                        Model = x.Model,
                        UserId = x.UserId,
                        OutputTokens = x.OutputTokens,
                        UseCase = x.UseCase,
                        Price = x.Model switch
                        {
                            "dall-e-3" => x.Count * .04f,
                            "dall-e-2" => x.Count * .018f,
                            "gpt-4-1106-preview" or "gpt-4-0125-preview" => .01f * x.InputTokens / 1000 + .03f * x.OutputTokens / 1000,
                            "gpt-4o" => (.01f * x.InputTokens / 1000 + .03f * x.OutputTokens / 1000) * 0.5f,
                            _ => null,
                        },
                        Category = x.UseCase switch
                        {
                            "own chatter" => x.Model switch
                            {
                                "gpt-4-1106-preview" or "gpt-4-0125-preview" or "gpt-4o" => "chat text",
                                _ => "chat images",
                            },
                            _ => x.Model switch
                            {
                                "gpt-4-1106-preview" or "gpt-4-0125-preview" or "gpt-4o" => "song text",
                                _ => "song images",
                            },
                        }
                    })
                    .OrderByDescending(x => x.CreatedOn)
                    .ToList(),
                UserTitles = await _songsDatabase.Users.ToDictionaryAsync(x => x.Id, x => x.Title),
            };
        }
    }
}
