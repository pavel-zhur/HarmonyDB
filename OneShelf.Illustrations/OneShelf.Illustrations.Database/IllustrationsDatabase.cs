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

    /// <summary>
    /// The new shelf's ids of items whose illustrations are kept under their old address (see <see cref="ShelfAlias"/>).
    /// </summary>
    public required DbSet<ShelfAlias> ShelfAliases { get; set; }

    /// <summary>
    /// What the new shelf's key stands for here: the old address its illustrations are kept under, when it has an alias;
    /// else the key itself.
    /// </summary>
    public async Task<string> GetShelfUrl(string key)
        => await ShelfAliases.Where(x => x.Key == key).Select(x => x.Url).SingleOrDefaultAsync() ?? key;

    /// <summary>
    /// Puts the aliases of the new shelf's keys: for each key, among its old addresses, the one with illustrations (the
    /// most, if several have them); a key with none is left without one, and so is a key that has illustrations of its
    /// own already. Putting the same again changes nothing.
    /// </summary>
    public async Task<ShelfAliasesResult> PutShelfAliases(IReadOnlyCollection<(string key, string url)> pairs)
    {
        var urls = pairs.Select(x => x.url).Distinct().ToList();
        var keys = pairs.Select(x => x.key).Distinct().ToList();
        var counts = await Illustrations.Where(x => urls.Contains(x.Url)).GroupBy(x => x.Url).Select(x => new { Url = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Url, x => x.Count);
        var prompted = (await Prompts.Where(x => urls.Contains(x.Url) || keys.Contains(x.Url)).Select(x => x.Url).Distinct().ToListAsync()).ToHashSet();
        var existing = await ShelfAliases.Where(x => keys.Contains(x.Key)).ToDictionaryAsync(x => x.Key);

        int added = 0, unchanged = 0, notIllustrated = 0, ownAlready = 0;
        foreach (var group in pairs.GroupBy(x => x.key))
        {
            var url = group.Select(x => x.url).Where(prompted.Contains)
                .OrderByDescending(x => counts.GetValueOrDefault(x)).ThenBy(x => x, StringComparer.Ordinal)
                .FirstOrDefault();
            if (url == null)
            {
                notIllustrated++;
                continue;
            }

            if (prompted.Contains(group.Key))
            {
                ownAlready++;
                continue;
            }

            if (existing.TryGetValue(group.Key, out var alias))
            {
                if (alias.Url == url)
                    unchanged++;
                else
                {
                    alias.Url = url;
                    added++;
                }

                continue;
            }

            ShelfAliases.Add(new() { Key = group.Key, Url = url });
            added++;
        }

        await SaveChangesAsync();
        ChangeTracker.Clear();

        var aliased = await ShelfAliases.Select(x => x.Url).Distinct().ToListAsync();
        var illustratedWithoutAlias = await Prompts.Select(x => x.Url).Distinct().Where(x => !aliased.Contains(x)).CountAsync();
        return new(added, unchanged, notIllustrated, ownAlready, illustratedWithoutAlias);
    }

    /// <summary>
    /// For each of the new shelf's keys that has illustrations (under its alias, when it has one), the first of them
    /// (the earliest, as <c>ShelfRead</c> orders them): one query.
    /// </summary>
    public async Task<List<ShelfCoverRow>> GetShelfCovers(IReadOnlyCollection<string> keys)
    {
        var distinct = keys.Distinct().ToArray();
        return await Database.SqlQuery<ShelfCoverRow>($"""
            SELECT k.key AS "Key", c."Id" AS "Id"
            FROM unnest({distinct}) AS k(key)
            LEFT JOIN "ShelfAliases" a ON a."Key" = k.key
            CROSS JOIN LATERAL (
                SELECT i."Id" FROM "Illustrations" i
                WHERE i."Url" = coalesce(a."Url", k.key)
                ORDER BY i."CreatedOn", i."AttemptIndex", i."PhotoIndex"
                LIMIT 1) c
            """).ToListAsync();
    }

    public sealed class ShelfCoverRow
    {
        public required string Key { get; init; }

        public Guid Id { get; init; }
    }

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

        modelBuilder.Entity<ShelfAlias>(x =>
        {
            // while both shelves run, an item has two names; this goes when the old shelf is retired (rows re-keyed)
            x.HasKey(x => x.Key);
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

/// <summary>
/// What putting the aliases did: keys aliased now, aliased so already, without an illustrated address, with illustrations
/// of their own; and how many illustrated addresses are left that no key stands for.
/// </summary>
public sealed record ShelfAliasesResult(int Added, int Unchanged, int NotIllustrated, int OwnAlready, int IllustratedWithoutAlias);