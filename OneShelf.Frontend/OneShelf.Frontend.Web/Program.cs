using System.Text.Json.Serialization;
using BlazorApplicationInsights;
using Blazored.LocalStorage;
using HarmonyDB.Index.Analysis;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OneShelf.Collectives.Api.Client;
using OneShelf.Frontend.Api.Client;
using OneShelf.Frontend.Web;
using OneShelf.Frontend.Web.IndexedDb;
using OneShelf.Frontend.Web.Interop;
using OneShelf.Frontend.Web.Models;
using OneShelf.Frontend.Web.Services;
using OneShelf.Frontend.Web.Services.Worker;
using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.WebWorkers;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddHttpClient();

builder.Services
    .AddBlazorJSRuntime(out var js)
    .AddWebWorkerService();

if (js.IsWorker)
{
    builder.Services
        .AddScoped<IService1, Service1>();
}

if (js.IsWindow)
{
    builder.Services
        .AddBlazoredLocalStorageAsSingleton(o =>
            o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip)
        .AddSingleton<Receiver>()
        .AddScoped(services => services.GetRequiredService<Receiver>().CreateInstance())
        .AddSingleton<IdentityProvider>()
        .AddSingleton<Api>()
        .AddSingleton<DataProvider>()
        .AddSingleton<ProgressionsCacheLoader>()
        .AddSingleton<InstantActions>()
        .AddSingleton<CollectionIndexProvider>()
        .AddSingleton<IllustrationsProvider>()
        .AddScoped<CollectionNavigation>()
        .AddScoped<JsFunctions>()
        .AddScoped<SearchContext>()
        .AddScoped<ProgressionsSearchContext>()
        .AddIndexAnalysis()
        .AddSingleton<ChordsCacheLoader>()
        .AddSingleton<Player>()
        .AddSingleton<Preferences>()
        .AddSingleton<LogoImageSequencer>()
        .AddSingleton<FingeringsProvider>()
        .AddCollectivesApiClient(builder.Configuration)
        .AddFrontendApiClient(builder.Configuration)
        .Configure<FrontendOptions>(o => builder.Configuration.GetSection(nameof(FrontendOptions)).Bind(o))
        .AddTransient<MyIndexedDb>()
        .AddSingleton<OptionalWebWorker>()
        .AddSingleton<IService2, Service2>();
}

builder.Services
    .AddBlazorApplicationInsights(config =>
    {
        config.ConnectionString =
            builder.Configuration.GetSection(nameof(FrontendOptions)).Get<FrontendOptions>()!
                .AppInsightsConnectionString;
    });

var host = builder.Build();

if (js.IsWindow)
{
    await host.Services.GetRequiredService<IdentityProvider>().Initialize();
    host.Services.GetRequiredService<OptionalWebWorker>().Initialize();
}

await host.BlazorJSRunAsync();