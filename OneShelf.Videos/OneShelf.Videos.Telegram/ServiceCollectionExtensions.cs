using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Telegram;
using OneShelf.Telegram.Commands;
using OneShelf.Telegram.PipelineHandlers;
using OneShelf.Videos.BusinessLogic;
using OneShelf.Videos.Telegram.Commands;
using OneShelf.Videos.Telegram.Model;
using OneShelf.Videos.Telegram.PipelineHandlers;
using OneShelf.Videos.Telegram.Services;

namespace OneShelf.Videos.Telegram;

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
                    .AddCommand<ViewTopics>()
                    .AddCommand<GetFileSize>()
                    .AddCommand<ListAlbums>()
                    
                    .AddPipelineHandlerInOrder<UpdatesCollector>()
                    .AddPipelineHandlerInOrder<DialogHandler>()
                );

        services
            .AddVideosBusinessLogic(configuration)
            .AddScoped<Scope>();

        return services;
    }
}