using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneShelf.Authorization.Api.Client;
using OneShelf.Collectives.Api.Models;
using OneShelf.Collectives.Api.Services;
using OneShelf.Collectives.Database;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Database.Songs;

var host = new HostBuilder()
    .ConfigureApi()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services
            .AddSongsDatabase()
            .AddCollectivesDatabase(context.Configuration)
            .AddAuthorizationApiClient(context.Configuration)
            .AddScoped<UrlsManager>()
            .Configure<CollectivesOptions>(o => context.Configuration.GetSection(nameof(CollectivesOptions)).Bind(o))
            .AddSecurityContext();
    })
    .Build();

host.Run();
