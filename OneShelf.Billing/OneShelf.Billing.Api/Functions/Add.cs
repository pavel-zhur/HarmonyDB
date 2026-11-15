using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Billing.Api.Model;
using OneShelf.Common.Api;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model;

namespace OneShelf.Billing.Api.Functions
{
    public class Add : FunctionBase<Usage>
    {
        private readonly SongsDatabase _songsDatabase;

        public Add(ILoggerFactory loggerFactory, SongsDatabase songsDatabase)
            : base(loggerFactory)
        {
            _songsDatabase = songsDatabase;
        }

        [Function(BillingApiUrls.Add, AuthorizationLevel.Function, "post")]
        public Task<IActionResult> Run(HttpRequest req, [FromBody] Usage usage) => RunHandler(usage);

        protected override async Task<IActionResult> ExecuteSuccessful(Usage usage)
        {
            Logger.LogInformation("Adding the usage: {usage}", JsonSerializer.Serialize(usage));

            if (usage.UserId.HasValue)
            {
                var existingUser = await _songsDatabase.Users.SingleOrDefaultAsync(x => x.Id == usage.UserId);
                if (existingUser == null)
                {
                    existingUser = new()
                    {
                        Tenant = new()
                        {
                            PrivateDescription = Tenant.PersonalPrivateDescription(usage.UserId.Value),
                        },
                        CreatedOn = DateTime.Now,
                        Id = usage.UserId.Value,
                        Title = "(unknown from billing)",
                    };

                    _songsDatabase.Users.Add(existingUser);
                    await _songsDatabase.SaveChangesAsyncX();
                }
            }

            _songsDatabase.BillingUsages.Add(new()
            {
                Count = usage.Count,
                AdditionalInfo = usage.AdditionalInfo,
                CreatedOn = usage.CreatedOn ?? DateTime.Now,
                InputTokens = usage.InputTokens,
                Model = usage.Model,
                UserId = usage.UserId,
                OutputTokens = usage.OutputTokens,
                UseCase = usage.UseCase,
                DomainId = usage.DomainId,
                ChatId = usage.ChatId,
            });

            await _songsDatabase.SaveChangesAsyncX();

            return new OkResult();
        }
    }
}
