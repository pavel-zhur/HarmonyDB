using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Authorization.Api.Client;
using OneShelf.Collectives.Api.Models;
using OneShelf.Collectives.Api.Services;
using OneShelf.Collectives.Database;
using OneShelf.Common.Api.Hosting;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Database.Songs;

namespace OneShelf.Collectives.Api;

public class CollectivesModule() : ApiModule("Collectives")
{
    public override void Configure(WebApplicationBuilder builder) => builder.Services
        .AddSongsDatabase()
        .AddCollectivesDatabase(builder.Configuration)
        .AddAuthorizationApiClient(builder.Configuration)
        .AddScoped<UrlsManager>()
        .Configure<CollectivesOptions>(o => builder.Configuration.GetSection(nameof(CollectivesOptions)).Bind(o))
        .AddSecurityContext();
}
