using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneShelf.Telegram.Polling.Model;
using OneShelf.Telegram.Polling.Services;

var host = Host.CreateDefaultBuilder(args)
    .UseDefaultServiceProvider((_, y) =>
    {
        y.ValidateOnBuild = true;
        y.ValidateScopes = true;
    })
    .UseConsoleLifetime()
    .ConfigureServices((context, services) =>
    {
        services
            .AddHostedService<ApiPoller>()
            .Configure<PollerOptions>(pollerOptions => context.Configuration.GetSection(nameof(PollerOptions)).Bind(pollerOptions))
            .AddHttpClient();
    })
    .Build();

await host.RunAsync();