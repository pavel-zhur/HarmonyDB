using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneShelf.Common.Api;
using OneShelf.Common.Database.Songs;

var host = new HostBuilder()
    .ConfigureApi()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddSongsDatabase();
    })
    .Build();

host.Run();
