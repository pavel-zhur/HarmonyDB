using HarmonyDB.Index.Analysis;
using HarmonyDB.Index.BusinessLogic.Caches;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Index.BusinessLogic;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIndexBusinessLogic(this IServiceCollection serviceCollection, IConfiguration configuration)
        => serviceCollection
            .AddSingleton<ProgressionsCache>()
            .AddSingleton<LoopsStatisticsCache>()
            .AddSingleton<TonalitiesCache>()
            .AddSingleton<IndexHeadersCache>()
            .AddSingleton<FullTextSearchCache>()
            .AddIndexAnalysis()
            .Configure<FileCacheBaseOptions>(o => configuration.GetSection(nameof(FileCacheBaseOptions)).Bind(o));
}