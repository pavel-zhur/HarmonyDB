using HarmonyDB.Index.Analysis.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.DependencyInjection.Logging;

namespace HarmonyDB.Index.Analysis.Tests;

public class Startup
{
    public void ConfigureHost(IHostBuilder hostBuilder)
    {
        hostBuilder
            .ConfigureHostConfiguration(x => x.AddJsonFile("appsettings.json"))
#if DEBUG
            .ConfigureHostConfiguration(x => x.AddJsonFile("appsettings.Test1.json"))
            .UseEnvironment("Development")
#endif
            .ConfigureServices(services =>
            {
                services
                    .AddSingleton<ChordDataParser>();
            });
    }
    public void Configure(IServiceProvider provider)
    {
        XunitTestOutputLoggerProvider.Register(provider);
    }
}