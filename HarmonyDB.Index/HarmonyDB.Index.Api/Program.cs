using HarmonyDB.Index.Api.Client;
using HarmonyDB.Index.Api.Models;
using HarmonyDB.Index.Api.Services;
using HarmonyDB.Index.BusinessLogic;
using HarmonyDB.Index.DownstreamApi.Client;
using HarmonyDB.Source.Api.Client;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nito.AsyncEx;
using OneShelf.Authorization.Api.Client;
using OneShelf.Collectives.Api.Client;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;

var host = new HostBuilder()
    .ConfigureApi()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services
            .AddCollectivesApiClient(context.Configuration)
            .AddAuthorizationApiClient(context.Configuration)
            .AddDownstreamApiClient(context.Configuration)
            .AddIndexApiClient(context.Configuration)
            .AddConcurrencyLimiter(context.Configuration)
            .AddScoped<CommonExecutions>()
            .AddScoped<InputParser>()
            .AddIndexBusinessLogic(context.Configuration)
            .AddSecurityContext()
            .Configure<IndexApiOptions>(o => context.Configuration.GetSection(nameof(IndexApiOptions)).Bind(o));
    })
    .Build();

host.Run();
