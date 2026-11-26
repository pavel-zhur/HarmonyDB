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

    /// <summary>
    /// The own chords authored by an opaque name of a person.
    /// </summary>
    public required DbSet<AuthoredCollective> AuthoredCollectives { get; set; }

    public async Task<List<AuthoredCollective>> GetAuthoredCollectives(IReadOnlyCollection<string> authors)
        => await AuthoredCollectives.AsNoTracking().Where(x => authors.Contains(x.Author)).ToListAsync();

    /// <param name="ids">The ids to read; null for all.</param>
    public async Task<List<AuthoredCollective>> GetAuthoredCollectives(IReadOnlyCollection<Guid>? ids)
        => await (ids == null ? AuthoredCollectives.AsNoTracking() : AuthoredCollectives.AsNoTracking().Where(x => ids.Contains(x.Id))).ToListAsync();

    public async Task<AuthoredCollective?> GetAuthoredCollective(Guid id)
        => await AuthoredCollectives.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);

    /// <returns>How many were removed.</returns>
    public async Task<int> RemoveAuthoredCollectives(IReadOnlyCollection<Guid> ids)
        => await AuthoredCollectives.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync();

    public async Task UpsertAuthoredCollective(AuthoredCollective collective)
    {
        Validator.ValidateObject(collective, new(collective));
        await this.UpsertAsync(collective);
    }

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

        modelBuilder.Entity<AuthoredCollective>(x =>
        {
            x.Property(x => x.Id).ValueGeneratedNever();
            x.Property(x => x.Versions).HasJsonbConversion();
            x.HasIndex(x => x.Author);
            x.HasIndex(x => x.LatestVisibility);
        });

        modelBuilder.ApplyPostgreSqlStoreTypes();
    }
}
