using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit.DependencyInjection.Logging;

namespace OneShelf.Telegram.Processor.Tests;

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