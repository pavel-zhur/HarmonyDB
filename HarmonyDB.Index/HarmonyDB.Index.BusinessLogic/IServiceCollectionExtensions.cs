using HarmonyDB.Index.BusinessLogic.Models;
using HarmonyDB.Index.BusinessLogic.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Index.BusinessLogic;

// ReSharper disable once InconsistentNaming
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddIndex(this IServiceCollection services, IConfiguration configuration)
        => services
            .Configure<IndexApiOptions>(options => configuration.Bind(nameof(IndexApiOptions), options))
            .AddScoped<SourceResolver>();
}