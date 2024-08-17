using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Telegram.Options;
using OneShelf.Telegram.Services;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTelegram<TScopedAbstractions, TSingletonAbstractions>(this IServiceCollection services, IConfiguration configuration, Action<TelegramOptionsBuilder> telegramOptionsBuilder)
        where TScopedAbstractions : class, IScopedAbstractions
        where TSingletonAbstractions : class, ISingletonAbstractions
    {
        services.Configure<TelegramTypes>(o =>
        {
            telegramOptionsBuilder(new(o));

            foreach (var command in o.Commands)
            {
                services.AddScoped(command);
            }

            foreach (var pipelineHandler in o.PipelineHandlers)
            {
                services.AddScoped(pipelineHandler);
            }
        });

        services
            .AddSingleton<DialogHandlerMemory>()
            .AddSingleton<PipelineMemory>()
            .AddScoped<IoFactory>()
            .AddScoped<IScopedAbstractions, TScopedAbstractions>()
            .AddSingleton<ISingletonAbstractions, TSingletonAbstractions>()
            .AddSingleton<AvailableCommands>()
            .AddScoped<Pipeline>()
            .AddScoped(serviceProvider => serviceProvider.GetRequiredService<IoFactory>().Io);

        return services;
    }
}