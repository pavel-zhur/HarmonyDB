using HarmonyDB.Index.Analysis.Em;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.DependencyInjection.Logging;

namespace HarmonyDB.Index.Analysis.Tests;

public class Startup
{
    public void ConfigureHost(IHostBuilder hostBuilder)
    {
        hostBuilder
            .ConfigureServices(services =>
            {
                services
                    .AddIndexAnalysis()
                    .AddIndexAnalysisEm()
                    .AddLogging(b => b.AddXunitOutput());
            });
    }
    public void Configure(IServiceProvider provider)
    {
    }
}