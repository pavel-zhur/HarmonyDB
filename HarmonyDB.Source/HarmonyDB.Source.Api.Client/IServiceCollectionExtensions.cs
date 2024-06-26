using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Api.Client;

namespace HarmonyDB.Source.Api.Client;

// ReSharper disable once InconsistentNaming
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddSourceApiClient(this IServiceCollection services, IConfiguration configuration)
        => services.AddApiClient<SourceApiClient>(configuration);
}