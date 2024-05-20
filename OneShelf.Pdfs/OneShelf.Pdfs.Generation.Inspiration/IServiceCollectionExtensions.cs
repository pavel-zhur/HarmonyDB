using Microsoft.Extensions.DependencyInjection;
using OneShelf.Common.Database.Songs;
using OneShelf.Pdfs.Generation.Inspiration.Services;

namespace OneShelf.Pdfs.Generation.Inspiration;

// ReSharper disable once InconsistentNaming
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddPdfsGenerationInspiration(this IServiceCollection services)
    {
        services
            .AddSongsDatabase()
            .AddScoped<InspirationGeneration>();

        return services;
    }
}