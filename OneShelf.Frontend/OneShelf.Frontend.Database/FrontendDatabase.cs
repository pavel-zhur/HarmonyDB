using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using OneShelf.Common.Database;
using OneShelf.Frontend.Database.Models;

namespace OneShelf.Frontend.Database;

/// <summary>
/// The generated PDFs, kept to be given again. PostgreSQL only.
/// </summary>
public class FrontendDatabase(DbContextOptions<FrontendDatabase> options) : DbContext(options)
{
    public const string PostgreSqlMigrationsAssembly = "OneShelf.Frontend.Database.PostgreSqlMigrations";

    public required DbSet<Pdf> Pdfs { get; set; }

    public async Task<Dictionary<string, Pdf>> GetPdfs(List<string> ids)
        => await Pdfs
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.ExternalId);

    public async Task<Dictionary<string, int>> GetPageCounts(List<string> ids)
        => await Pdfs
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.ExternalId, x.PageCount })
            .ToDictionaryAsync(x => x.ExternalId, x => x.PageCount);

    public async Task AddOrUpdatePdfs(IReadOnlyCollection<Pdf> pdfs)
    {
        foreach (var pdf in pdfs)
        {
            Validator.ValidateObject(pdf, new(pdf));
        }

        foreach (var pdf in pdfs)
        {
            await this.UpsertAsync(pdf);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyPostgreSqlStoreTypes();
    }
}
