using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.Database.Songs.Services;
using OneShelf.Telegram.Ai.Model;
using Version = OneShelf.Common.Database.Songs.Model.Version;

namespace OneShelf.Common.Database.Songs;

public class SongsDatabase : DbContext, IInteractionsRepository<InteractionType>
{
    private readonly SongsDatabaseMemory _songsDatabaseMemory;
    private readonly int _versionCopy;

    public SongsDatabase(DbContextOptions<SongsDatabase> options, SongsDatabaseMemory songsDatabaseMemory) : base(options)
    {
        _songsDatabaseMemory = songsDatabaseMemory;
        _versionCopy = _songsDatabaseMemory.Get();
    }

    public required DbSet<Tenant> Tenants { get; set; }

    public required DbSet<Song> Songs { get; set; }

    public required DbSet<SongContent> SongContents { get; set; }

    public required DbSet<ArtistSynonym> ArtistSynonyms { get; set; }

    public required DbSet<Like> Likes { get; set; }

    public required DbSet<Comment> Comments { get; set; }

    public required DbSet<User> Users { get; set; }

    public required DbSet<Artist> Artists { get; set; }

    public required DbSet<Message> Messages { get; set; }

    public required DbSet<SameSongGroup> SameSongGroups { get; set; }

    public required DbSet<Version> Versions { get; set; }

    public required DbSet<Interaction> Interactions { get; set; }

    public required DbSet<VisitedSearch> VisitedSearches { get; set; }

    public required DbSet<VisitedChords> VisitedChords { get; set; }
    
    public required DbSet<LikeCategory> LikeCategories { get; set; }
    
    public required DbSet<BillingUsage> BillingUsages { get; set; }

    public async Task<int> GetNextSongIndex(int tenantId)
    {
        return (await Database.SqlQuery<int>($"exec getnextsongindex @tenantid={new SqlParameter("tenantid", SqlDbType.Int)
        {
            Value = tenantId
        }}").ToListAsync()).Single();
    }

    public Task<int> SaveChangesAsyncX(bool fullTextSearchReset = false, CancellationToken cancellationToken = new())
    {
        if (fullTextSearchReset)
        {
            _songsDatabaseMemory.Advance();
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    [Obsolete]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = new())
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
    {
        return base.SaveChangesAsync(cancellationToken);
    }

    [Obsolete]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = new())
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
    {
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public int GetVersion() => _versionCopy;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>()
            .HasMany(x => x.Artists)
            .WithOne(x => x.Tenant)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Tenant>()
            .HasMany(x => x.LikeCategories)
            .WithOne(x => x.Tenant)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Tenant>()
            .HasMany(x => x.Messages)
            .WithOne(x => x.Tenant)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Tenant>()
            .HasMany(x => x.Songs)
            .WithOne(x => x.Tenant)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Tenant>()
            .HasMany(x => x.Users)
            .WithOne(x => x.Tenant)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Song>()
            .Property(x => x.CategoryOverride)
            .HasConversion<string>();

        modelBuilder.Entity<Song>()
            .Property(x => x.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Artist>()
            .Property(x => x.CategoryOverride)
            .HasConversion<string>();

        modelBuilder.Entity<Message>()
            .Property(x => x.Type)
            .HasConversion<string>();

        modelBuilder.Entity<Message>()
            .Property(x => x.Category)
            .HasConversion<string?>();

        modelBuilder.Entity<Interaction>()
            .Property(x => x.InteractionType)
            .HasConversion<string?>();

        modelBuilder.Entity<LikeCategory>()
            .Property(x => x.Access)
            .HasConversion<string?>();

        modelBuilder.Entity<Message>()
            .HasIndex(l => new
            {
                l.Type,
                l.ArtistId,
                l.Category,
                l.Part,
                l.SongId,
            })
            .IsUnique();

        modelBuilder.Entity<Song>()
            .HasOne(x => x.CreatedByUser)
            .WithMany(x => x.SongsCreated)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Song>()
            .HasOne(x => x.RedirectToSong)
            .WithMany(x => x.RedirectsFromSongs);

        modelBuilder.Entity<Like>()
            .HasOne(x => x.Version)
            .WithMany(x => x.Likes)
            .HasForeignKey(x => new
            {
                x.VersionId, x.SongId,
            })
            .HasPrincipalKey(x => new
            {
                x.Id, x.SongId,
            });

        modelBuilder.Entity<Comment>()
            .HasOne(x => x.Version)
            .WithMany(x => x.Comments)
            .HasForeignKey(x => new
            {
                x.VersionId, x.SongId,
            })
            .HasPrincipalKey(x => new
            {
                x.Id, x.SongId,
            });

        modelBuilder.Entity<Like>()
            .HasIndex(x => new
            {
                x.SongId,
                x.UserId,
                x.LikeCategoryId,
            })
            .IsUnique()
            .HasFilter("[VersionId] IS NULL");

        modelBuilder.Entity<Version>()
            .Property(x => x.CollectiveType)
            .HasConversion<string?>();
    }

    #region IInteractionsRepository

    Task<List<IInteraction<InteractionType>>> IInteractionsRepository<InteractionType>.Get(Func<IQueryable<IInteraction<InteractionType>>, IQueryable<IInteraction<InteractionType>>> query)
    {
        return query(Interactions).ToListAsync();
    }

    async Task IInteractionsRepository<InteractionType>.Add(List<IInteraction<InteractionType>> interactions)
    {
        Interactions.AddRange(interactions.Cast<Interaction>());
        await SaveChangesAsyncX();
    }

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterMessage => InteractionType.OwnChatterMessage;

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterMemoryPoint => InteractionType.OwnChatterMemoryPoint;

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterResetDialog => InteractionType.OwnChatterResetDialog;

    InteractionType IInteractionsRepository<InteractionType>.ImagesLimit => InteractionType.ImagesLimit;

    InteractionType IInteractionsRepository<InteractionType>.ImagesSuccess => InteractionType.ImagesSuccess;

    #endregion
}