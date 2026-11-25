using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Billing.Api.Client;
using OneShelf.Common.OpenAi.Models;
using OneShelf.Common.OpenAi.Services;

namespace OneShelf.Common.OpenAi;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAiOptions>(options => configuration.Bind(nameof(OpenAiOptions), options));

        services
            .AddScoped<DialogRunner>()
            .AddScoped<Transcriber>()
            .AddScoped<SoraVideoGenerator>()
            .AddScoped<VeoVideoGenerator>()
            .AddScoped<MusicGenerator>()
            .AddBillingApiClient(configuration)
            .AddHttpClient();

        // the client of the direct calls to the OpenAI API: it carries the key, so it logs nothing of its requests
        services.AddHttpClient(DirectClientName, x => x.Timeout = TimeSpan.FromMinutes(5)).RemoveAllLoggers();

        return services;
    }

    public const string DirectClientName = "openai-direct";
}