using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Api.Client;

namespace HarmonyDB.Index.Api.Client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection
        AddIndexApiClient(this IServiceCollection services, IConfiguration configuration) =>
        services.AddApiClient<IndexApiClient>(configuration);
}