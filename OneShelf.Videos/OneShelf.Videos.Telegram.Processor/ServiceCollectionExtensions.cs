using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Telegram;
using OneShelf.Telegram.Commands;
using OneShelf.Telegram.PipelineHandlers;
using OneShelf.Videos.BusinessLogic;
using OneShelf.Videos.Telegram.Processor.Commands;
using OneShelf.Videos.Telegram.Processor.Model;
using OneShelf.Videos.Telegram.Processor.PipelineHandlers;
using OneShelf.Videos.Telegram.Processor.Services;

namespace OneShelf.Videos.Telegram.Processor;

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
                    .AddCommand<ViewChats>()
                    .AddCommand<GetFileSize>()
                    .AddCommand<ListAlbums>()
                    .AddCommand<ShowHandlers>()
                    .AddCommand<DownloadAll>()
                    
                    .AddPipelineHandlerInOrder<UpdatesCollector>()
                    .AddPipelineHandlerInOrder<TagsHandler>()
                    .AddPipelineHandlerInOrder<VideosCollector>()
                    .AddPipelineHandlerInOrder<DialogHandler>()
                );

        services
            .AddVideosBusinessLogic(configuration)
            .AddSingleton<VideosCollectorMemory>()
            .AddSingleton<HandlersConstructor>()
            .AddScoped<Scope>();

        return services;
    }
}