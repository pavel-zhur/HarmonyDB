using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Api.Client;

namespace OneShelf.Illustrations.Api.Client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIllustrationsApiClient(this IServiceCollection services, IConfiguration configuration)
        => services.AddApiClient<IllustrationsApiClient, IllustrationsApiClientOptions>(configuration);
}