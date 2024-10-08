﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.OpenAi;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Processor.Services;
using OneShelf.OneDog.Processor.Services.Commands;
using OneShelf.OneDog.Processor.Services.Commands.Admin;
using OneShelf.OneDog.Processor.Services.Commands.DomainAdmin;
using OneShelf.OneDog.Processor.Services.PipelineHandlers;
using OneShelf.Telegram;
using OneShelf.Telegram.Commands;
using OneShelf.Telegram.Options;
using OneShelf.Telegram.PipelineHandlers;

namespace OneShelf.OneDog.Processor;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProcessor(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramOptions>(options => configuration.Bind(nameof(TelegramOptions), options));

        services
            .AddDogDatabase()
            .AddTelegram<ScopedAbstractions, SingletonAbstractions>(configuration, o =>
                o
                    .AddCommand<UpdateCommands>()
                    .AddCommand<Help>()
                    .AddCommand<Nothing>()
                    .AddCommand<Start>()
                    
                    .AddCommand<Temp>()
                    .AddCommand<ViewBilling>()
                    .AddCommand<ConfigureChatGpt>()
                    .AddCommand<ConfigureDog>()

                    .AddPipelineHandlerInOrder<UsersCollector>()
                    .AddPipelineHandlerInOrder<ChatsCollector>()
                    .AddPipelineHandlerInOrder<DialogHandler>()
                    .AddPipelineHandlerInOrder<AiDialogHandler>()
                );

        services
            .AddScoped<DogContext>()
            .AddOpenAi(configuration);

        return services;
    }
}