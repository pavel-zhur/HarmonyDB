using System.Text.Json;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Billing.Api.Model;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model;

namespace OneShelf.Billing.Api.Functions
{
    public class Add
    {
        private readonly ILogger<Add> _logger;
        private readonly SongsDatabase _songsDatabase;

        public Add(ILogger<Add> logger, SongsDatabase songsDatabase)
        {
            _logger = logger;
            _songsDatabase = songsDatabase;
        }

        [Function(nameof(Add))]
        public async Task Run([QueueTrigger(BillingApiUrls.InputQueueName)] QueueMessage message)
        {
            _logger.LogInformation($"C# Queue trigger function processed: {message.MessageText}");

            var usage = JsonSerializer.Deserialize<Usage>(message.MessageText);

            if (usage!.UserId.HasValue)
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
        }
    }
}
