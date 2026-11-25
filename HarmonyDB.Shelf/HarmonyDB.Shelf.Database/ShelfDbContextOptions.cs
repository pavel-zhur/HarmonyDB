using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Database;

public static class ShelfDbContextOptions
{
    /// <summary>
    /// The name of the connection string of the shelf's database.
    /// </summary>
    public const string ConnectionStringName = "Shelf";

    /// <summary>
    /// Connects the context to PostgreSQL the one way the app, the tools and the migrations all use.
    /// </summary>
    public static TBuilder UseShelfDatabase<TBuilder>(this TBuilder builder, string connectionString)
        where TBuilder : DbContextOptionsBuilder
    {
        builder
            .UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention();
        return builder;
    }
}
