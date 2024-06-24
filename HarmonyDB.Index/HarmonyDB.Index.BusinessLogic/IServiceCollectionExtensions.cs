using HarmonyDB.Index.Analysis;
using HarmonyDB.Index.BusinessLogic.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Index.BusinessLogic;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddIndexBusinessLogic(this IServiceCollection serviceCollection)
        => serviceCollection
            .AddSingleton<ProgressionsCache>()
            .AddSingleton<LoopsStatisticsCache>()
            .AddSingleton<IndexHeadersCache>()
            .AddIndexAnalysis();
}