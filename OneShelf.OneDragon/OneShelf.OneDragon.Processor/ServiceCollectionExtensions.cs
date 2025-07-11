﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.OpenAi;
using OneShelf.OneDragon.Database;
using OneShelf.OneDragon.Processor.Commands;
using OneShelf.OneDragon.Processor.Model;
using OneShelf.OneDragon.Processor.PipelineHandlers;
using OneShelf.OneDragon.Processor.Services;
using OneShelf.Telegram;
using OneShelf.Telegram.Commands;
using OneShelf.Telegram.PipelineHandlers;

namespace OneShelf.OneDragon.Processor;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProcessor(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramOptions>(options => configuration.Bind(nameof(TelegramOptions), options));

        services
            .AddTelegram<ScopedAbstractions, SingletonAbstractions>(configuration, o => 
                o
                    .AddCommand<Help>()
                    .AddCommand<Start>()
                    
                    .AddCommand<UpdateCommands>()
                    .AddCommand<ViewBilling>()
                    .AddCommand<Amnesia>()
                    .AddCommand<Images>()
                    .AddCommand<Videos>()
                    .AddCommand<Songs>()
                    
                    .AddPipelineHandlerInOrder<UpdatesCollector>()
                    .AddPipelineHandlerInOrder<UsersCollector>()
                    .AddPipelineHandlerInOrder<ChatsCollector>()
                    .AddPipelineHandlerInOrder<DialogHandler>()
                    .AddPipelineHandlerInOrder<AiDialogHandler>()
                );

        services
            .AddDragonDatabase()
            .AddOpenAi(configuration)
            .AddScoped<Availability>()
            .AddScoped<DragonScope>();

        return services;
    }
}