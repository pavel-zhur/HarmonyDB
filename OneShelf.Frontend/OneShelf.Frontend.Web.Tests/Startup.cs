using HarmonyDB.Index.Analysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneShelf.Frontend.Web.Services;
using Xunit.DependencyInjection.Logging;

namespace OneShelf.Frontend.Web.Tests;

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
                    .AddIndexAnalysis();
            });
    }
    public void Configure(IServiceProvider provider)
    {
        XunitTestOutputLoggerProvider.Register(provider);
    }
}