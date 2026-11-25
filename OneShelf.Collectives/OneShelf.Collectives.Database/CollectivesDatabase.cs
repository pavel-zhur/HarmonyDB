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
    /// The own chords of the new shelf's accounts.
    /// </summary>
    public required DbSet<AccountCollective> AccountCollectives { get; set; }

    public async Task<List<AccountCollective>> GetAccountCollectives(Guid accountId)
        => await AccountCollectives.AsNoTracking().Where(x => x.AccountId == accountId).ToListAsync();

    /// <param name="ids">The ids to read; null for all.</param>
    public async Task<List<AccountCollective>> GetAccountCollectives(IReadOnlyCollection<Guid>? ids)
        => await (ids == null ? AccountCollectives.AsNoTracking() : AccountCollectives.AsNoTracking().Where(x => ids.Contains(x.Id))).ToListAsync();

    public async Task<AccountCollective?> GetAccountCollective(Guid id)
        => await AccountCollectives.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);

    /// <returns>How many were moved.</returns>
    public async Task<int> MoveAccountCollectives(Guid fromAccountId, Guid toAccountId)
        => await AccountCollectives.Where(x => x.AccountId == fromAccountId)
            .ExecuteUpdateAsync(x => x.SetProperty(c => c.AccountId, toAccountId));

    public async Task UpsertAccountCollective(AccountCollective collective)
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

        modelBuilder.Entity<AccountCollective>(x =>
        {
            x.Property(x => x.Id).ValueGeneratedNever();
            x.Property(x => x.Versions).HasJsonbConversion();
            x.HasIndex(x => x.AccountId);
            x.HasIndex(x => x.LatestVisibility);
        });

        modelBuilder.ApplyPostgreSqlStoreTypes();
    }
}
