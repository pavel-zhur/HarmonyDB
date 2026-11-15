using HarmonyDB.Index.Api.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneShelf.Common.Api;
using OneShelf.Common.OpenAi;
using OneShelf.Illustrations.Api.Options;
using OneShelf.Illustrations.Api.Services;
using OneShelf.Illustrations.Database;

var host = new HostBuilder()
    .ConfigureApi()
    .ConfigureServices((host, services) =>
    {
        services.AddIndexApiClient(host.Configuration);
        services.AddOpenAi(host.Configuration);
        services.AddIllustrationsDatabase(host.Configuration);

        services.AddScoped<AllReader>();
        services
            .AddOptions<IllustrationsApiOptions>()
            .Bind(host.Configuration.GetSection(nameof(IllustrationsApiOptions)))
            .Validate(x => x.PublicEndpoint != null, $"{nameof(IllustrationsApiOptions)}:{nameof(IllustrationsApiOptions.PublicEndpoint)} is required.");

    })
    .Build();

host.Run();
