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
    /// <summary>
    /// The scope of the internal clients (configuration <c>Collectives:InternalClients</c>) that may call the functions of
    /// the own chords for the new shelf (<see cref="Functions.VInternal.OwnFunctions"/>).
    /// </summary>
    public const string InternalScope = "collectives";

    public override void Configure(WebApplicationBuilder builder) => builder.Services
        .AddSongsDatabase()
        .AddCollectivesDatabase(builder.Configuration)
        .AddAuthorizationApiClient(builder.Configuration)
        .AddScoped<UrlsManager>()
        .Configure<CollectivesOptions>(o => builder.Configuration.GetSection(nameof(CollectivesOptions)).Bind(o))
        .AddSecurityContext();
}
