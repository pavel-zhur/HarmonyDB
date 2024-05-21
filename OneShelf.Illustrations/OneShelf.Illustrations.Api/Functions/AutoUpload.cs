using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Illustrations.Api.Services;

namespace OneShelf.Illustrations.Api.Functions
{
    public class AutoUpload
    {
        private readonly AutoUploader _autoUploader;
        private readonly ILogger _logger;

        public AutoUpload(ILoggerFactory loggerFactory, AutoUploader autoUploader)
        {
            _autoUploader = autoUploader;
            _logger = loggerFactory.CreateLogger<AutoUpload>();
        }

        [Function("AutoUpload")]
        public async Task Run([TimerTrigger("0 0 */2 * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            
            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }

            await _autoUploader.Go();
        }
    }
}
