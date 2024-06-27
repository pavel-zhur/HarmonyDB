using Microsoft.Extensions.Options;

namespace HarmonyDB.Playground.Web;

public class LocalizationPipeline
{
    public void Configure(IApplicationBuilder app, IOptions<RequestLocalizationOptions> options)
    {
        app.UseRequestLocalization(options.Value);
    }
}