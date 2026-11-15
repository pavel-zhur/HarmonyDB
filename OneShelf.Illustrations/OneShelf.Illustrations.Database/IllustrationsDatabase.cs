using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using OneShelf.Common.Database;
using OneShelf.Illustrations.Database.Models;
using OneShelf.Illustrations.Database.Services;

namespace OneShelf.Illustrations.Database;

/// <summary>
/// The prompts and the illustrations made by them: each illustration's JPEG and its resized copies, served to browsers.
/// The nested lists and the trace of the prompts are kept in jsonb columns. PostgreSQL only.
/// </summary>
public class IllustrationsDatabase(DbContextOptions<IllustrationsDatabase> options, ImagesProcessor imagesProcessor) : DbContext(options)
{
    public const string PostgreSqlMigrationsAssembly = "OneShelf.Illustrations.Database.PostgreSqlMigrations";

    /// <summary>
    /// The sides of the square copies every illustration has.
    /// </summary>
    public static readonly IReadOnlyList<int> CopySizes = [1024, 512, 256, 128];

    public required DbSet<IllustrationsPrompts> Prompts { get; set; }

    public required DbSet<Illustration> Illustrations { get; set; }

    public required DbSet<IllustrationCopy> IllustrationCopies { get; set; }

    public async Task<List<IllustrationsPrompts>> GetPrompts(string? url = null)
    {
        var prompts = Prompts.AsNoTracking();
        if (url != null) prompts = prompts.Where(x => x.Url == url);
        return await prompts.ToListAsync();
    }

    public async Task<List<string>> GetNonEmptyUrls()
        => await Prompts.Select(x => x.Url).Distinct().ToListAsync();

    public async Task<List<IllustrationHeader>> GetIllustrationHeaders(string? url = null)
    {
        var illustrations = Illustrations.AsQueryable();
        if (url != null) illustrations = illustrations.Where(x => x.Url == url);
        return await illustrations
            .Select(x => new IllustrationHeader
            {
                CreatedOn = x.CreatedOn,
                AttemptIndex = x.AttemptIndex,
                Id = x.Id,
                PhotoIndex = x.PhotoIndex,
                Url = x.Url,
                PromptsId = x.PromptsId,
            })
            .ToListAsync();
    }

    public async Task<Illustration?> GetIllustration(Guid id)
        => await Illustrations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);

    /// <summary>
    /// The JPEG of a copy, null when there is no such illustration or size.
    /// </summary>
    public async Task<byte[]?> GetIllustrationCopy(Guid id, int size)
        => await IllustrationCopies
            .Where(x => x.IllustrationId == id && x.Size == size)
            .Select(x => x.Image)
            .SingleOrDefaultAsync();

    public async Task AddPrompts(IllustrationsPrompts illustrationsPrompts)
    {
        Validator.ValidateObject(illustrationsPrompts, new(illustrationsPrompts));
        Prompts.Add(illustrationsPrompts);
        await SaveChangesAsync();

        // the store hands out snapshots and keeps nothing tracked
        Entry(illustrationsPrompts).State = EntityState.Detached;
    }

    /// <summary>
    /// Adds the illustration with its copies, made here from its image.
    /// </summary>
    public async Task AddIllustration(Illustration illustration)
    {
        Validator.ValidateObject(illustration, new(illustration));

        var copies = CopySizes.Zip(imagesProcessor.Resize(illustration.Image, CopySizes))
            .Select(x => new IllustrationCopy
            {
                IllustrationId = illustration.Id,
                Size = x.First,
                Image = x.Second,
            })
            .ToList();

        Illustrations.Add(illustration);
        IllustrationCopies.AddRange(copies);
        await SaveChangesAsync();

        Entry(illustration).State = EntityState.Detached;
        copies.ForEach(x => Entry(x).State = EntityState.Detached);
    }

    public async Task PublishCustomSystemMessage(Guid illustrationId, int specialSystemMessage, string? alterationKey)
    {
        var illustration = await GetIllustration(illustrationId);
        var promptsId = illustration!.PromptsId;

        var existingPrompts = await GetPrompts(illustration.Url);
        if (existingPrompts.Any(x => x.SpecialSystemMessage == specialSystemMessage && x.AlterationKey == alterationKey && x.Id != promptsId))
            throw new SupportedException("Such version already exists for that url.");

        var targetPrompt = existingPrompts.Single(x => x.Id == promptsId);
        if ((targetPrompt.SpecialSystemMessage.HasValue && targetPrompt.SpecialSystemMessage != specialSystemMessage)
            || (targetPrompt.AlterationKey != null && targetPrompt.AlterationKey != alterationKey))
            throw new SupportedException("Another special system message or alteration key is already assigned to the corresponding prompt.");

        await Prompts
            .Where(x => x.Id == promptsId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(x => x.SpecialSystemMessage, specialSystemMessage)
                .SetProperty(x => x.AlterationKey, alterationKey)
                .SetProperty(x => x.CustomSystemMessage, (string?)null));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<IllustrationsPrompts>(x =>
        {
            x.ToTable(nameof(IllustrationsPrompts));
            x.Property(x => x.Id).ValueGeneratedNever();
            x.Property(x => x.Prompts).HasJsonbConversion();
            x.Property(x => x.Trace).HasJsonbConversion();
            x.HasIndex(x => x.Url);
        });

        modelBuilder.Entity<Illustration>(x =>
        {
            x.Property(x => x.Id).ValueGeneratedNever();
            x.HasIndex(x => x.Url);
        });

        modelBuilder.Entity<IllustrationCopy>(x =>
        {
            x.HasKey(x => new { x.IllustrationId, x.Size });
            x.HasOne<Illustration>().WithMany().HasForeignKey(x => x.IllustrationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.ApplyPostgreSqlStoreTypes();
    }
}
