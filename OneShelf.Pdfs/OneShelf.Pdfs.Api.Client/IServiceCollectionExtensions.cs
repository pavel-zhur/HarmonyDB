using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OneShelf.Pdfs.Api.Client
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddPdfsApiClient(this IServiceCollection services, IConfiguration configuration) => services
            .AddScoped<PdfsApiClient>()
            .AddHttpClient()
            .Configure<PdfsApiClientOptions>(options => configuration.Bind(nameof(PdfsApiClientOptions), options));
    }
}