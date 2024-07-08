using HarmonyDB.Index.Analysis.Em;
using HarmonyDB.Index.Analysis.Em.Services;
using HarmonyDB.Index.Analysis.Tests.Em;
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
                    .AddScoped<Output>()
                    .AddLogging(b => b.AddXunitOutput());
            });
    }
    public void Configure(IServiceProvider provider)
    {
    }
}