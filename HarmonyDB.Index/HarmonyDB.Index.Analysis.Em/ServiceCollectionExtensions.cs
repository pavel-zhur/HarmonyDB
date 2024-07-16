using HarmonyDB.Index.Analysis.Em.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Index.Analysis.Em;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIndexAnalysisEm(this IServiceCollection services) => services
        .AddSingleton<MusicAnalyzer>();
}