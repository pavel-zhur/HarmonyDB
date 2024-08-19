using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.OneDog.Database;

namespace OneShelf.OneDog.Runner.Functions.Functions
{
    public class Warmups
    {
        private readonly DogDatabase _dogDatabase;
        private readonly ILogger _logger;

        public Warmups(ILoggerFactory loggerFactory, DogDatabase dogDatabase)
        {
            _dogDatabase = dogDatabase;
            _logger = loggerFactory.CreateLogger<Warmups>();
        }

        [Function("Warmups")]
        public async Task Run([TimerTrigger("0 */4 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}, domains: {await _dogDatabase.Domains.CountAsync()}");
            
            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
