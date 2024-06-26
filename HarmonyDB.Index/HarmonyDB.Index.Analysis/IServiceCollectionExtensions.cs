using HarmonyDB.Index.Analysis.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Index.Analysis;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddIndexAnalysis(this IServiceCollection services) => services
        .AddSingleton<ChordDataParser>()
        .AddSingleton<ProgressionsBuilder>()
        .AddSingleton<InputParser>()
        .AddSingleton<ProgressionsSearch>();
}