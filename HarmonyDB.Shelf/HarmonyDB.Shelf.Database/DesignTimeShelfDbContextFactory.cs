using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HarmonyDB.Shelf.Database;

/// <summary>
/// For the EF tools (dotnet ef migrations add …, run from this project). Adding a migration needs no database; to
/// apply one with the tools, give the connection string in SHELF_CONNECTION_STRING.
/// </summary>
public sealed class DesignTimeShelfDbContextFactory : IDesignTimeDbContextFactory<ShelfDbContext>
{
    public ShelfDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<ShelfDbContext>()
            .UseShelfDatabase(Environment.GetEnvironmentVariable("SHELF_CONNECTION_STRING") ?? "Host=localhost;Database=shelf")
            .Options);
}
