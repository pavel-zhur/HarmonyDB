using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Telegram.Services;

namespace OneShelf.Telegram;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTelegram(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddSingleton<DialogHandlerMemory>();

        return services;
    }
}