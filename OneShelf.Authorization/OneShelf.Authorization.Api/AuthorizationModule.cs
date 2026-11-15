using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Authorization.Api.Model;
using OneShelf.Authorization.Api.Services;
using OneShelf.Common.Api.Hosting;
using OneShelf.Common.Database.Songs;

namespace OneShelf.Authorization.Api;

public class AuthorizationModule() : ApiModule("Authorization")
{
    public override void Configure(WebApplicationBuilder builder) => builder.Services
        .AddSongsDatabase()
        .AddScoped<AuthorizationChecker>()
        .Configure<AuthorizationOptions>(options => builder.Configuration.Bind(nameof(AuthorizationOptions), options));
}
