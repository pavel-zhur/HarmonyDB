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
            .ConfigureServices(services =>
            {
            });
    }
    public void Configure(IServiceProvider provider)
    {
        XunitTestOutputLoggerProvider.Register(provider);
    }
}