using OneShelf.Common.Api.Hosting;

namespace OneShelf.Pdfs.Api;

/// <summary>
/// Turns HTML into PDF with the Chrome that the Syncfusion converter carries. Its settings: <c>SyncFusionLicenseKey</c>,
/// and <c>BlinkBinariesPath</c> when the Chrome to use is elsewhere (see <see cref="Convert"/>).
/// </summary>
public class PdfsModule() : ApiModule("Pdfs")
{
    /// <summary>
    /// The scope of the internal clients (configuration <c>Pdfs:InternalClients</c>) that may call the functions besides
    /// the callers with a function key.
    /// </summary>
    public const string InternalScope = "pdf";

    public override void Configure(WebApplicationBuilder builder)
    {
    }
}
