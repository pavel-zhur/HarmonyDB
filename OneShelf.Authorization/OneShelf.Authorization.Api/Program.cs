using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneShelf.Authorization.Api.Model;
using OneShelf.Authorization.Api.Services;
using OneShelf.Common.Database.Songs;
using System.Text.Json.Serialization;
using System.Text.Json;
using OneShelf.Common.Api;

var host = new HostBuilder()
    .ConfigureApi()
    .ConfigureServices((context, services) => services
        .AddSongsDatabase()
        .AddScoped<AuthorizationChecker>()
        .Configure<AuthorizationOptions>(options => context.Configuration.Bind(nameof(AuthorizationOptions), options)))
    .Build();

host.Run();