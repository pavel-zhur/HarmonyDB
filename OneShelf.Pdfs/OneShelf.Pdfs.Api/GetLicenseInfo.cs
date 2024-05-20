using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Syncfusion.Licensing;

namespace OneShelf.Pdfs.Api
{
    public class GetLicenseInfo
    {
        private readonly IConfiguration _configuration;

        public GetLicenseInfo(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("GetLicenseInfo")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            FusionLicenseProvider.IsBoldLicenseValidation = true;

            var info = FusionLicenseProvider.ExtractBase64LicenseKey(_configuration.GetValue<string>(Convert.SyncFusionLicenseKey));

            var result = Syncfusion.Licensing.SyncfusionLicenseProvider.ValidateLicense(Platform.ASPNETCore, out var message);

            return new OkObjectResult(new
            {
                info,
                result,
                message,
            });
        }
    }
}
