using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HarmonyDB.Analyses.Database;

/// <summary>
/// For the EF tools (dotnet ef migrations add …, run from this project): adding a migration needs no database.
/// </summary>
public sealed class DesignTimeAnalysesDatabaseFactory : IDesignTimeDbContextFactory<AnalysesDatabase>
{
    public AnalysesDatabase CreateDbContext(string[] args)
        => new((DbContextOptions<AnalysesDatabase>)AnalysesDatabase.Configure(new DbContextOptionsBuilder<AnalysesDatabase>(), null).Options);
}
