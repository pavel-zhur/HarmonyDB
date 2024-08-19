using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneShelf.Common.Api;
using OneShelf.OneDog.Processor;

var host = new HostBuilder()
    .ConfigureApi()
    .UseDefaultServiceProvider((_, y) =>
    {
        y.ValidateOnBuild = true;
        y.ValidateScopes = true;
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services
            .AddProcessor(context.Configuration);
    })
    .Build();

host.Run();
