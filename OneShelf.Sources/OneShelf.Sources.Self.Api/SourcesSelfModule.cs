using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Authorization.Api.Client;
using OneShelf.Collectives.Api.Client;
using OneShelf.Common.Api.Hosting;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Sources.Self.Api.Models;
using OneShelf.Sources.Self.Api.Services;
using OneShelf.Sources.Self.Api.Services.V2;

namespace OneShelf.Sources.Self.Api;

public class SourcesSelfModule() : ApiModule("SourcesSelf")
{
    public override void Configure(WebApplicationBuilder builder)
    {
        builder.Services
            .Configure<SelfApiOptions>(x => builder.Configuration.GetSection(nameof(SelfApiOptions)).Bind(x))
            .AddScoped<StructureParser>()
            .AddScoped<MetadataBuilder>()
            .AddCollectivesApiClient(builder.Configuration)
            .AddAuthorizationApiClient(builder.Configuration)
            .AddSecurityContext();

        // the source contract, version 2
        builder.Services
            .AddOptions<SelfV2Options>()
            .Bind(builder.Configuration.GetSection(SelfV2Options.SectionName))
            .Validate(x => (x.IconName == null) == (x.IconPath == null), $"{SelfV2Options.SectionName}: the icon's name and file go together.")
            .Validate(x => x.IconPath == null || File.Exists(x.IconPath), $"{SelfV2Options.SectionName}: the icon's file does not exist.")
            .Validate(x => x.IconName == null || x.IconName.StartsWith(SelfV2Source.IdPrefix, StringComparison.Ordinal), $"{SelfV2Options.SectionName}: the icon's name does not begin with {SelfV2Source.IdPrefix}.")
            .Validate(x => (x.ShelfAccountsEndpoint == null) == string.IsNullOrEmpty(x.ShelfAccountsKey), $"{SelfV2Options.SectionName}: the shelf's accounts endpoint and key go together.")
            .Validate(x => x.ShelfAccountsEndpoint == null || (x.ShelfAccountsEndpoint.IsAbsoluteUri && x.ShelfAccountsEndpoint.AbsolutePath.EndsWith('/')),
                $"{SelfV2Options.SectionName}: the shelf's accounts endpoint is not an absolute address ending with a slash.")
            .Validate(x => x.ShelfAccountsTimeout > TimeSpan.Zero && x.ShelfAccountsCacheTime > TimeSpan.Zero, $"{SelfV2Options.SectionName}: the shelf's accounts timeout or cache time is not positive.")
            .ValidateOnStart();
        builder.Services.AddMemoryCache();
        builder.Services.AddHttpClient<ShelfAccountsClient>();
        builder.Services.AddScoped<SelfV2Source>();
    }
}
