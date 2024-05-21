using HarmonyDB.Index.Api.Functions.V1;
using HarmonyDB.Index.Api.Services;
using HarmonyDB.Index.BusinessLogic;
using HarmonyDB.Index.BusinessLogic.Models;
using HarmonyDB.Sources.Api.Client;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneShelf.Authorization.Api.Client;
using OneShelf.Collectives.Api.Client;
using OneShelf.Common.Api;

var host = new HostBuilder()
    .ConfigureApi()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services
            .AddIndex(context.Configuration)
            .AddCollectivesApiClient(context.Configuration)
            .Configure<IndexApiOptions>(x => context.Configuration.GetSection(nameof(IndexApiOptions)).Bind(x))
            .AddAuthorizationApiClient(context.Configuration)
            .AddSourcesApiClient(context.Configuration)
            .AddScoped<CommonExecutions>();
    })
    .Build();

host.Run();
