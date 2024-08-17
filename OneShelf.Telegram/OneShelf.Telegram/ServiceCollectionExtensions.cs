using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Telegram.Services;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTelegram<TScopedAbstractions, TSingletonAbstractions>(this IServiceCollection services, IConfiguration configuration)
        where TScopedAbstractions : class, IScopedAbstractions
        where TSingletonAbstractions : class, ISingletonAbstractions
    {
        services
            .AddSingleton<DialogHandlerMemory>()
            .AddSingleton<PipelineMemory>()
            .AddScoped<IoFactory>()
            .AddScoped<IScopedAbstractions, TScopedAbstractions>()
            .AddSingleton<ISingletonAbstractions, TSingletonAbstractions>();

        return services;
    }
}