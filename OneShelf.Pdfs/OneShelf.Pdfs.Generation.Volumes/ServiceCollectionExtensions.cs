using Microsoft.Extensions.DependencyInjection;
using OneShelf.Pdfs.Generation.Volumes.Services;

namespace OneShelf.Pdfs.Generation.Volumes;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPdfsGenerationVolumes(this IServiceCollection services)
    {
        services
            .AddScoped<VolumesGeneration>();

        return services;
    }
}