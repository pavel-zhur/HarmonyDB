using HarmonyDB.Index.Api.Client;
using HarmonyDB.Source.Api.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Authorization.Api.Client;
using OneShelf.Common.Api.Hosting;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Database.Songs;
using OneShelf.Frontend.Api.AuthorizationQuickCheck;
using OneShelf.Frontend.Api.Model;
using OneShelf.Frontend.Api.Services;
using OneShelf.Frontend.Database;
using OneShelf.Illustrations.Api.Client;
using OneShelf.Pdfs.Api.Client;
using OneShelf.Pdfs.Generation.Inspiration;
using OneShelf.Pdfs.Generation.Volumes;
using OneShelf.Sources.Self.Api.Client;

namespace OneShelf.Frontend.Api;

public class FrontendModule() : ApiModule("Frontend")
{
    public override void Configure(WebApplicationBuilder builder) => builder.Services
        .AddAuthorizationQuickCheckOptions(builder.Configuration)
        .AddSongsDatabase()
        .AddIndexApiClient(builder.Configuration)
        .AddSourceApiClient(builder.Configuration)
        .AddAuthorizationApiClient(builder.Configuration)
        .AddScoped<CollectionReaderV3>()
        .AddScoped<IllustrationsReader>()
        .Configure<FrontendOptions>(options => builder.Configuration.Bind(nameof(FrontendOptions), options))
        .AddPdfsGenerationInspiration()
        .AddPdfsGenerationVolumes()
        .AddPdfsApiClient(builder.Configuration)
        .AddIllustrationsApiClient(builder.Configuration)
        .AddFrontendDatabase(builder.Configuration)
        .AddSelfApiClient(builder.Configuration)
        .AddSecurityContext();
}
