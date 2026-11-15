using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Authorization.Api.Client;
using OneShelf.Collectives.Api.Client;
using OneShelf.Common.Api.Hosting;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Sources.Self.Api.Models;
using OneShelf.Sources.Self.Api.Services;

namespace OneShelf.Sources.Self.Api;

public class SourcesSelfModule() : ApiModule("SourcesSelf")
{
    public override void Configure(WebApplicationBuilder builder) => builder.Services
        .Configure<SelfApiOptions>(x => builder.Configuration.GetSection(nameof(SelfApiOptions)).Bind(x))
        .AddScoped<StructureParser>()
        .AddScoped<MetadataBuilder>()
        .AddCollectivesApiClient(builder.Configuration)
        .AddAuthorizationApiClient(builder.Configuration)
        .AddSecurityContext();
}
