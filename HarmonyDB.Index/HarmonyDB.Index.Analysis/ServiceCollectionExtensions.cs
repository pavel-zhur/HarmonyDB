using HarmonyDB.Index.Analysis.Em;
using HarmonyDB.Index.Analysis.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Index.Analysis;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIndexAnalysis(this IServiceCollection services) => services
        .AddSingleton<ChordDataParser>()
        .AddSingleton<ProgressionsBuilder>()
        .AddSingleton<ProgressionsVisualizer>()
        .AddSingleton<IndexExtractor>()
        .AddSingleton<InputParser>()
        .AddSingleton<ProgressionsSearch>()
        .AddIndexAnalysisEm();
}