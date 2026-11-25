#pragma warning disable EF1001 // the migrations' internals are read on purpose: to compare the model with them.

using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace HarmonyDB.Shelf.Tests.Database;

public sealed class ModelTests
{
    [Fact]
    public void MigrationsMatchTheModel()
    {
        using var context = CreateContext();
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        if (snapshot is IMutableModel mutable)
            snapshot = mutable.FinalizeModel();
        snapshot = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot);

        var differences = context.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshot.GetRelationalModel(),
            context.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.Empty(differences);
    }

    [Fact]
    public void EveryVersionedTableHasItsTrigger()
    {
        using var context = CreateContext();
        var versioned = context.Model.GetEntityTypes()
            .Where(x => typeof(IVersioned).IsAssignableFrom(x.ClrType))
            .Select(x => x.GetTableName()!)
            .Order()
            .ToList();

        var migrations = context.GetService<IMigrationsAssembly>();
        var sql = string.Join("\n", migrations.Migrations.Values
            .Select(x => migrations.CreateMigration(x, "Npgsql.EntityFrameworkCore.PostgreSQL"))
            .SelectMany(x => x.UpOperations)
            .OfType<SqlOperation>()
            .Select(x => x.Sql));

        Assert.NotEmpty(versioned);
        Assert.All(versioned, table => Assert.Contains($"CREATE TRIGGER {table}_version BEFORE INSERT OR UPDATE ON {table}", sql));
    }

    private static ShelfDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ShelfDbContext>().UseShelfDatabase("Host=localhost;Database=shelf-tests").Options);
}
