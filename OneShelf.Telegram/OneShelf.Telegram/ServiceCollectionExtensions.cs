﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        services.Configure<TelegramOptions>(options => configuration.Bind(nameof(TelegramOptions), options));
        services.Configure<TelegramTypes>(o => telegramOptionsBuilder(new(o)));

        services
            .AddSingleton<DialogHandlerMemory>()
            .AddSingleton<PipelineMemory>()
            .AddScoped<IoFactory>()
            .AddScoped<IScopedAbstractions, TScopedAbstractions>()
            .AddSingleton<ISingletonAbstractions, TSingletonAbstractions>()
            .AddScoped<TelegramContext>()
            .AddSingleton<AvailableCommands>()
            .AddScoped<Pipeline>()
            .AddScoped(serviceProvider => serviceProvider.GetRequiredService<IoFactory>().Io);

        TelegramTypes telegramTypes = new();
        telegramOptionsBuilder(new(telegramTypes));

        foreach (var command in telegramTypes.Commands)
        {
            services.AddScoped(command);
        }

        foreach (var pipelineHandler in telegramTypes.PipelineHandlers)
        {
            services.AddScoped(pipelineHandler);
        }

        return services;
    }
}