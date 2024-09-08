using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OneShelf.Videos.Database;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVideosDatabase(this IServiceCollection serviceCollection, IConfiguration configuration)
        => serviceCollection
            .AddDbContext<VideosDatabase>(o => o.UseSqlServer(configuration.GetConnectionString(nameof(VideosDatabase))));
}