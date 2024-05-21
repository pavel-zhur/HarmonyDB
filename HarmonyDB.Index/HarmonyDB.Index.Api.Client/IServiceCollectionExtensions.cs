using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Api.Client;

namespace HarmonyDB.Index.Api.Client;

// ReSharper disable once InconsistentNaming
public static class IServiceCollectionExtensions
{
    public static IServiceCollection
        AddIndexApiClient(this IServiceCollection services, IConfiguration configuration) =>
        services.AddApiClient<IndexApiClient>(configuration);
}