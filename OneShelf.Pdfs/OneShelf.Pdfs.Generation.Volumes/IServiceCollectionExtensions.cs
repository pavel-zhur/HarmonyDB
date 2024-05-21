using Microsoft.Extensions.DependencyInjection;
using OneShelf.Pdfs.Generation.Volumes.Services;

namespace OneShelf.Pdfs.Generation.Volumes;

// ReSharper disable once InconsistentNaming
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddPdfsGenerationVolumes(this IServiceCollection services)
    {
        services
            .AddScoped<VolumesGeneration>();

        return services;
    }
}