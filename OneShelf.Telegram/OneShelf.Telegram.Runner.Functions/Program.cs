using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneShelf.Common.Api;
using OneShelf.Telegram.Processor;

var host = new HostBuilder()
    .ConfigureApi()
    .UseDefaultServiceProvider((x, y) =>
    {
        y.ValidateOnBuild = true;
        y.ValidateScopes = true;
    })
    .ConfigureLogging(x => x.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None))
    .ConfigureServices((context, services) =>
    {
        services
            .AddProcessor(context.Configuration);
    })
    .Build();

await host.RunAsync();
