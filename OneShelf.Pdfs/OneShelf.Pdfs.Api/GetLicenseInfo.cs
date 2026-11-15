using Microsoft.AspNetCore.Mvc;
using OneShelf.Common.Api;
using Syncfusion.Licensing;

namespace OneShelf.Pdfs.Api
{
    public class GetLicenseInfo
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GetLicenseInfo> _logger;

        public GetLicenseInfo(IConfiguration configuration, ILogger<GetLicenseInfo> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [Function("GetLicenseInfo", AuthorizationLevel.Function, "get")]
        public IActionResult Run()
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

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
