using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Telegram.Services;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTelegram<TTelegramAbstractions>(this IServiceCollection services, IConfiguration configuration)
        where TTelegramAbstractions : class, ITelegramAbstractions
    {
        services
            .AddSingleton<DialogHandlerMemory>()
            .AddSingleton<PipelineMemory>()
            .AddScoped<IoFactory>()
            .AddSingleton<ITelegramAbstractions, TTelegramAbstractions>();

        return services;
    }
}