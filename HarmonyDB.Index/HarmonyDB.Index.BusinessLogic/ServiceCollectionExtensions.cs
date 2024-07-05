using HarmonyDB.Index.Analysis;
using HarmonyDB.Index.BusinessLogic.Models;
using HarmonyDB.Index.BusinessLogic.Services;
using HarmonyDB.Index.BusinessLogic.Services.Caches;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Index.BusinessLogic;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIndexBusinessLogic(this IServiceCollection serviceCollection, IConfiguration configuration)
        => serviceCollection
            .AddSingleton<ProgressionsCache>()
            .AddSingleton<LoopsStatisticsCache>()
            .AddSingleton<TonalitiesIndexCache>()
            .AddSingleton<IndexHeadersCache>()
            .AddSingleton<FullTextSearchCache>()
            .AddIndexAnalysis()
            .Configure<FileCacheBaseOptions>(o => configuration.GetSection(nameof(FileCacheBaseOptions)).Bind(o));
}