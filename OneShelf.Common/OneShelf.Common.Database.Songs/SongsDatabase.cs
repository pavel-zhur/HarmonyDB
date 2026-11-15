using System.Data;
using System.Linq.Expressions;
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
    public const string PostgreSqlMigrationsAssembly = "OneShelf.Common.Database.Songs.PostgreSqlMigrations";

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

    /// <summary>
    /// Takes the next song index of the tenant. The tenant's row stays locked until the transaction ends.
    /// </summary>
    public async Task<int> GetNextSongIndex(int tenantId)
    {
        if (Database.IsNpgsql())
        {
            return (await Database.SqlQuery<int>($"""
                update "Tenants" set "LatestUsedIndex" = "LatestUsedIndex" + 1 where "Id" = {tenantId}
                returning "LatestUsedIndex" as "Value"
                """).ToListAsync()).Single();
        }

        return (await Database.SqlQuery<int>($"exec getnextsongindex @tenantid={new SqlParameter("tenantid", SqlDbType.Int)
        {
            Value = tenantId
        }}").ToListAsync()).Single();
    }

    /// <summary>
    /// The chords views of all users, without a view followed by the same chords within ten minutes
    /// (by minute boundaries crossed, as T-SQL's DATEDIFF counts them). The letter case does not matter.
    /// </summary>
    public IQueryable<VisitedChords> GetVisitedChordsWithoutRepeats()
    {
        if (Database.IsNpgsql())
        {
            return VisitedChords.FromSqlRaw("""
                select "Id", "ViewedOn", "UserId", "Uri", "SongId", "ExternalId", "SearchQuery", "Transpose", "Artists", "Title", "Source"
                from (
                	select
                		*,
                		row(lower("Uri"), "SongId", lower("ExternalId"), lower("SearchQuery"), lower("Artists"), lower("Title"), lower("Source")) as chords,
                		lead(row(lower("Uri"), "SongId", lower("ExternalId"), lower("SearchQuery"), lower("Artists"), lower("Title"), lower("Source"))) over w as next,
                		lead("ViewedOn") over w as next_viewed_on
                	from "VisitedChords"
                	window w as (partition by "UserId" order by "ViewedOn", "Id")
                ) x
                where chords is distinct from next
                	or date_trunc('minute', next_viewed_on) - date_trunc('minute', "ViewedOn") > interval '10 minutes'
                """);
        }

        return VisitedChords.FromSqlRaw(@"

select [Id]
      ,[ViewedOn]
      ,[UserId]
      ,[Uri]
      ,[SongId]
      ,[ExternalId]
      ,[SearchQuery]
      ,[Transpose]
      ,[Artists]
      ,[Title]
      ,[Source]
from (

	select
		*,
		lead(c) over (partition by userid order by viewedon, id) c2,
		lead(viewedon) over (partition by userid order by viewedon, id) v2
	from (
		select checksum(uri, songid, externalid, SearchQuery, Artists, title, source) c, *
		from visitedchords
	) x

) x
where c <> isnull(c2, 0) or DATEDIFF(minute, v2, viewedon) < -10

");
    }

    /// <summary>
    /// The searches of all users, without a search followed within ten minutes by one that continues or shortens it
    /// (by minute boundaries crossed, as T-SQL's DATEDIFF counts them). The letter case does not matter.
    /// </summary>
    public IQueryable<VisitedSearch> GetVisitedSearchesWithoutRepeats()
    {
        if (Database.IsNpgsql())
        {
            return VisitedSearches.FromSqlRaw("""
                select "Id", "SearchedOn", "Query", "UserId"
                from (
                	select
                		*,
                		lower("Query") as lower_query,
                		lead(lower("Query")) over w as next_lower_query,
                		lead("SearchedOn") over w as next_searched_on
                	from "VisitedSearches"
                	window w as (partition by "UserId" order by "SearchedOn", "Id")
                ) x
                where next_lower_query is null
                	or not (starts_with(lower_query, next_lower_query) or starts_with(next_lower_query, lower_query))
                	or date_trunc('minute', next_searched_on) - date_trunc('minute', "SearchedOn") > interval '10 minutes'
                """);
        }

        return VisitedSearches.FromSqlRaw(@"

select Id, SearchedOn, Query, UserId
from (

	select
		*,
		lead(Query) over (partition by userid order by SearchedOn, id) q2,
		lead(SearchedOn) over (partition by userid order by SearchedOn, id) v2
	from VisitedSearches x

) x
where q2 is null or
(
	left(query, case when len(query) < len(q2) then len(query) else len(q2) end) <>
	left(q2,	case when len(query) < len(q2) then len(query) else len(q2) end)
)
or DATEDIFF(minute, v2, SearchedOn) < -10

");
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

        // one song like per user and category, and only one without a category: here NULL equals NULL, as SQL Server has it
        var songLikes = modelBuilder.Entity<Like>()
            .HasIndex(x => new
            {
                x.SongId,
                x.UserId,
                x.LikeCategoryId,
            })
            .IsUnique();

        if (Database.IsNpgsql())
        {
            songLikes
                .HasFilter("\"VersionId\" IS NULL")
                .AreNullsDistinct(false);
        }
        else
        {
            songLikes.HasFilter("[VersionId] IS NULL");
        }

        modelBuilder.Entity<Version>()
            .Property(x => x.CollectiveType)
            .HasConversion<string?>();

        if (Database.IsNpgsql())
        {
            modelBuilder.ApplyPostgreSqlStoreTypes();
        }
    }

    #region IInteractionsRepository

    public void Initialize(Expression<Func<IInteraction<InteractionType>, bool>> scopeFilter)
    {
        throw new InvalidOperationException();
    }

    Task<List<IInteraction<InteractionType>>> IInteractionsRepository<InteractionType>.Get(Func<IQueryable<IInteraction<InteractionType>>, IQueryable<IInteraction<InteractionType>>> query)
    {
        return query(Interactions).ToListAsync();
    }

    async Task IInteractionsRepository<InteractionType>.Add(List<IInteraction<InteractionType>> interactions)
    {
        Interactions.AddRange(interactions.Cast<Interaction>());
        await SaveChangesAsyncX();
    }

    async Task IInteractionsRepository<InteractionType>.Update(IInteraction<InteractionType> interaction)
    {
        await SaveChangesAsyncX();
    }

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterMessage => InteractionType.OwnChatterMessage;

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterImageMessage => InteractionType.OwnChatterImageMessage;

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterMemoryPoint => InteractionType.OwnChatterMemoryPoint;

    InteractionType IInteractionsRepository<InteractionType>.OwnChatterResetDialog => InteractionType.OwnChatterResetDialog;

    InteractionType IInteractionsRepository<InteractionType>.ImagesLimit => InteractionType.ImagesLimit;

    InteractionType IInteractionsRepository<InteractionType>.ImagesSuccess => InteractionType.ImagesSuccess;
    InteractionType IInteractionsRepository<InteractionType>.VideosLimit => InteractionType.VideosLimit;
    InteractionType IInteractionsRepository<InteractionType>.VideosSuccess => InteractionType.VideosSuccess;
    InteractionType IInteractionsRepository<InteractionType>.SongsLimit => InteractionType.MusicLimit;
    InteractionType IInteractionsRepository<InteractionType>.SongsSuccess => InteractionType.MusicSuccess;

    InteractionType IInteractionsRepository<InteractionType>.Audio => InteractionType.OwnChatterAudio;

    #endregion
}