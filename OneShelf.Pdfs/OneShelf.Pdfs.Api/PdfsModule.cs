using OneShelf.Common.Api.Hosting;

namespace OneShelf.Pdfs.Api;

/// <summary>
/// Turns HTML into PDF with the Chrome that the Syncfusion converter carries. Its settings: <c>SyncFusionLicenseKey</c>,
/// and <c>BlinkBinariesPath</c> when the Chrome to use is elsewhere (see <see cref="Convert"/>).
/// </summary>
public class PdfsModule() : ApiModule("Pdfs")
{
    public override void Configure(WebApplicationBuilder builder)
    {
    }
}
