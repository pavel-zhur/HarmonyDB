using HarmonyDB.Shelf.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Foundation.Database;

public static class DatabaseExtensions
{
    /// <summary>
    /// The setting that makes the app apply the database's migrations when it starts.
    /// </summary>
    public const string MigrateOnStartKey = "Shelf:Database:MigrateOnStart";

    /// <summary>
    /// The database's context; its connection string is checked at start.
    /// </summary>
    public static IServiceCollection AddShelfDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ShelfDatabaseOptions>()
            .Configure(x => x.ConnectionString = configuration.GetConnectionString(ShelfDbContextOptions.ConnectionStringName))
            .Validate(x => !string.IsNullOrWhiteSpace(x.ConnectionString), $"The connection string '{ShelfDbContextOptions.ConnectionStringName}' is not set.")
            .ValidateOnStart();

        return services.AddDbContext<ShelfDbContext>((provider, options) =>
            options.UseShelfDatabase(provider.GetRequiredService<IOptions<ShelfDatabaseOptions>>().Value.ConnectionString!));
    }

    public static async Task MigrateShelfDatabaseIfConfiguredAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>(MigrateOnStartKey))
            return;

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ShelfDbContext>().Database.MigrateAsync();
    }

    private sealed class ShelfDatabaseOptions
    {
        public string? ConnectionString { get; set; }
    }
}
