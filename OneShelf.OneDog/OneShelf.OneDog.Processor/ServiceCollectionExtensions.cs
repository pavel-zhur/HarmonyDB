using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.OpenAi;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Processor.Model;
using OneShelf.OneDog.Processor.Services;
using OneShelf.OneDog.Processor.Services.Commands;
using OneShelf.OneDog.Processor.Services.Commands.Admin;
using OneShelf.OneDog.Processor.Services.Commands.DomainAdmin;
using OneShelf.OneDog.Processor.Services.PipelineHandlers;

namespace OneShelf.OneDog.Processor;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProcessor(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramOptions>(options => configuration.Bind(nameof(TelegramOptions), options));

        services
            .AddDogDatabase();

        services
            .AddScoped<ChannelActions>()
            .AddScoped<Pipeline>()
            .AddScoped<IoFactory>()
            .AddScoped(serviceProvider => serviceProvider.GetRequiredService<IoFactory>().Io)

            .AddSingleton<PipelineMemory>()
            .AddSingleton<DialogHandlerMemory>()

            .AddScoped<DialogHandler>()
            .AddScoped<OwnChatterHandler>()
            .AddScoped<UsersCollector>()
            .AddScoped<ChatsCollector>()

            .AddScoped<Help>()
            .AddScoped<Nothing>()
            .AddScoped<Start>()

            .AddScoped<Temp>()
            .AddScoped<ViewBilling>()
            .AddScoped<UpdateCommands>()
            .AddScoped<ConfigureChatGpt>()
            .AddScoped<ConfigureDog>()

            .AddScoped<ScopeAwareness>()
            
            .AddOpenAi(configuration);

        return services;
    }
}