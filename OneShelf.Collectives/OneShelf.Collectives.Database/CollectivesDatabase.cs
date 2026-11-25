using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using OneShelf.Collectives.Database.Models;
using OneShelf.Common.Database;

namespace OneShelf.Collectives.Database;

/// <summary>
/// The collectives, one row each, their versions kept together in a jsonb column. PostgreSQL only.
/// </summary>
public class CollectivesDatabase(DbContextOptions<CollectivesDatabase> options) : DbContext(options)
{
    public const string PostgreSqlMigrationsAssembly = "OneShelf.Collectives.Database.PostgreSqlMigrations";

    public required DbSet<Collective> Collectives { get; set; }

    public async Task<List<Collective>> GetCollectives(long? createdByUserId = null, CollectiveVisibility? visibility = null)
    {
        var collectives = Collectives.AsNoTracking();
        if (createdByUserId != null) collectives = collectives.Where(x => x.CreatedByUserId == createdByUserId);
        if (visibility != null) collectives = collectives.Where(x => x.LatestVisibility == visibility);
        return await collectives.ToListAsync();
    }

    public async Task<List<Collective>> GetCollectives(IReadOnlyCollection<Guid> ids)
        => await Collectives.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync();

    public async Task<Collective?> GetCollective(Guid id)
        => await Collectives.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);

    public async Task UpsertCollective(Collective collective)
    {
        Validator.ValidateObject(collective, new(collective));
        await this.UpsertAsync(collective);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Collective>(x =>
        {
            x.Property(x => x.Id).ValueGeneratedNever();
            x.Property(x => x.Versions).HasJsonbConversion();
            x.HasIndex(x => x.CreatedByUserId);
            x.HasIndex(x => x.LatestVisibility);
        });

        modelBuilder.ApplyPostgreSqlStoreTypes();
    }
}
