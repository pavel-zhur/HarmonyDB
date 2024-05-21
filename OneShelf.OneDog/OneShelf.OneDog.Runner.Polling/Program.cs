using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Runner.Polling.Model;
using OneShelf.OneDog.Runner.Polling.Services;

var host = Host.CreateDefaultBuilder(args)
    .UseDefaultServiceProvider((x, y) =>
    {
        y.ValidateOnBuild = true;
        y.ValidateScopes = true;
    })
    .UseConsoleLifetime()
    .ConfigureHostConfiguration(configuration => configuration.AddJsonFile("appsettings.Secrets.json", true))
    .ConfigureServices((context, services) =>
    {
        services
            .AddHostedService<ApiPoller>()
            .AddDogDatabase()
            .Configure<PollerOptions>(pollerOptions => context.Configuration.GetSection(nameof(PollerOptions)).Bind(pollerOptions))
            .AddHttpClient();
    })
    .Build();

await host.RunAsync();