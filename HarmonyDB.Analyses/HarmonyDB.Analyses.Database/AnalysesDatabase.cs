using HarmonyDB.Analyses.Database.Model;
using Microsoft.EntityFrameworkCore;
using OneShelf.Common.Database;

namespace HarmonyDB.Analyses.Database;

/// <summary>
/// The analyses, their content and their files' records, in the analyses' own schema of the common database: the
/// connection is made as the analyses' own role, whose search_path is that schema, so the tables and the migrations'
/// history are there and nowhere else. PostgreSQL only; tables and columns in snake case.
/// </summary>
public class AnalysesDatabase(DbContextOptions<AnalysesDatabase> options) : DbContext(options)
{
    /// <summary>
    /// The name of the connection string.
    /// </summary>
    public const string ConnectionStringName = nameof(AnalysesDatabase);

    public DbSet<Analysis> Analyses => Set<Analysis>();

    public DbSet<AnalysisContent> AnalysisContents => Set<AnalysisContent>();

    public DbSet<AnalysisFile> AnalysisFiles => Set<AnalysisFile>();

    /// <summary>
    /// Connects the context the one way the module, the tests and the migrations all use.
    /// </summary>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string? connectionString)
        => builder
            .UseDatabase(DatabaseProvider.PostgreSql, connectionString, typeof(AnalysesDatabase).Assembly.GetName().Name!)
            .UseSnakeCaseNamingConvention();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AnalysesDatabase).Assembly);
}
