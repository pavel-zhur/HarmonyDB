using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneShelf.Billing.Api.Client;
using OneShelf.Common.OpenAi.Models;
using OneShelf.Common.OpenAi.Services;

namespace OneShelf.Common.OpenAi;

// ReSharper disable once InconsistentNaming
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAiOptions>(options => configuration.Bind(nameof(OpenAiOptions), options));

        services
            .AddScoped<DialogRunner>()
            .AddBillingApiClient(configuration);

        return services;
    }
}