using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.OneDragon.Database;

namespace OneShelf.OneDragon.Runner.Functions.Functions
{
    public class Warmups
    {
        private readonly DragonDatabase _dragonDatabase;
        private readonly ILogger _logger;

        public Warmups(ILoggerFactory loggerFactory, DragonDatabase dragonDatabase)
        {
            _dragonDatabase = dragonDatabase;
            _logger = loggerFactory.CreateLogger<Warmups>();
        }

        [Function("Warmups")]
        public async Task Run([TimerTrigger("0 */4 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}, chats: {await _dragonDatabase.Chats.CountAsync()}");
            
            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
