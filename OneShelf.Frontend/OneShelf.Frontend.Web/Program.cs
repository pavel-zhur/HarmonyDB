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
using SpawnDev.BlazorJS;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddHttpClient();

builder.Services
    .AddBlazoredLocalStorageAsSingleton(o => o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip)
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
    .AddBlazorApplicationInsights(config =>
    {
        config.ConnectionString = builder.Configuration.GetSection(nameof(FrontendOptions)).Get<FrontendOptions>()!.AppInsightsConnectionString;
    })
    .AddTransient<MyIndexedDb>()
    .AddBlazorJSRuntime();

var host = builder.Build();

await host.Services.GetRequiredService<IdentityProvider>().Initialize();

await host.RunAsync();