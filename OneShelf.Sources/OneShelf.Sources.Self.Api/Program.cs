using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneShelf.Authorization.Api.Client;
using OneShelf.Collectives.Api.Client;
using OneShelf.Common.Api;
using OneShelf.Sources.Self.Api.Models;
using OneShelf.Sources.Self.Api.Services;

var host = new HostBuilder()
    .ConfigureApi()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services
            .Configure<SelfApiOptions>(x => context.Configuration.GetSection(nameof(SelfApiOptions)).Bind(x))
            .AddScoped<StructureParser>()
            .AddScoped<MetadataBuilder>()
            .AddCollectivesApiClient(context.Configuration)
            .AddAuthorizationApiClient(context.Configuration);
    })
    .Build();

host.Run();
