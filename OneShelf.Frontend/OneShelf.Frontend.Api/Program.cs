using HarmonyDB.Index.Api.Client;
using HarmonyDB.Source.Api.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneShelf.Authorization.Api.Client;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Database.Songs;
using OneShelf.Frontend.Api.AuthorizationQuickCheck;
using OneShelf.Frontend.Api.Model;
using OneShelf.Frontend.Api.Services;
using OneShelf.Frontend.Database.Cosmos;
using OneShelf.Illustrations.Api.Client;
using OneShelf.Pdfs.Api.Client;
using OneShelf.Pdfs.Generation.Inspiration;
using OneShelf.Pdfs.Generation.Volumes;
using OneShelf.Sources.Self.Api.Client;

var host = new HostBuilder()
    .ConfigureApi()
    .ConfigureServices((context, services) => services
        .AddAuthorizationQuickCheckOptions(context.Configuration)
        .AddSongsDatabase()
        .AddIndexApiClient(context.Configuration)
        .AddSourceApiClient(context.Configuration)
        .AddAuthorizationApiClient(context.Configuration)
        .AddScoped<CollectionReaderV3>()
        .AddScoped<IllustrationsReader>()
        .Configure<FrontendOptions>(options => context.Configuration.Bind(nameof(FrontendOptions), options))
        .AddPdfsGenerationInspiration()
        .AddPdfsGenerationVolumes()
        .AddPdfsApiClient(context.Configuration)
        .AddIllustrationsApiClient(context.Configuration)
        .AddFrontendCosmosDatabase(context.Configuration)
        .AddSelfApiClient(context.Configuration)
        .AddSecurityContext())
    .Build();

host.Run();