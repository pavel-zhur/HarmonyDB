using System.Linq.Expressions;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HarmonyDB.Shelf.Database;

/// <summary>
/// The shelf's own database (PostgreSQL). Tables and columns are in snake case; enums are kept as snake-case text.
/// The rules of removing (tombstones, uniqueness, undo, deleting a profile or a playlist) are on <see cref="ITombstoned"/>.
/// </summary>
public class ShelfDbContext(DbContextOptions<ShelfDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountLogin> AccountLogins => Set<AccountLogin>();
    public DbSet<AccountMerge> AccountMerges => Set<AccountMerge>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SignInFlow> SignInFlows => Set<SignInFlow>();
    public DbSet<AccountPermission> AccountPermissions => Set<AccountPermission>();
    public DbSet<MusicLink> MusicLinks => Set<MusicLink>();
    public DbSet<AccountSettings> AccountSettings => Set<AccountSettings>();

    public DbSet<ChordsItem> Chords => Set<ChordsItem>();
    public DbSet<Recording> Recordings => Set<Recording>();
    public DbSet<ChordsRecording> ChordsRecordings => Set<ChordsRecording>();

    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<PlaylistMember> PlaylistMembers => Set<PlaylistMember>();
    public DbSet<PlaylistSong> PlaylistSongs => Set<PlaylistSong>();
    public DbSet<PlaylistSongItem> PlaylistSongItems => Set<PlaylistSongItem>();
    public DbSet<PlaylistSongPreference> PlaylistSongPreferences => Set<PlaylistSongPreference>();
    public DbSet<PlaylistImportTombstone> PlaylistImportTombstones => Set<PlaylistImportTombstone>();
    public DbSet<PlaylistInvitation> PlaylistInvitations => Set<PlaylistInvitation>();
    public DbSet<PlaylistFollow> PlaylistFollows => Set<PlaylistFollow>();

    public DbSet<Mark> Marks => Set<Mark>();
    public DbSet<Transposition> Transpositions => Set<Transposition>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<RecentItem> Recent => Set<RecentItem>();

    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();

    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobOutput> JobOutputs => Set<JobOutput>();
    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();
    public DbSet<UnreadLink> UnreadLinks => Set<UnreadLink>();

    public DbSet<OAuthClient> OAuthClients => Set<OAuthClient>();
    public DbSet<OAuthGrant> OAuthGrants => Set<OAuthGrant>();
    public DbSet<OAuthAuthorizationCode> OAuthAuthorizationCodes => Set<OAuthAuthorizationCode>();
    public DbSet<OAuthAuthorizationRequest> OAuthAuthorizationRequests => Set<OAuthAuthorizationRequest>();
    public DbSet<OAuthToken> OAuthTokens => Set<OAuthToken>();
    public DbSet<PersonalKey> PersonalKeys => Set<PersonalKey>();

    /// <summary>
    /// The watermark of changes for a reader: every transaction below it is finished. A reader that read at this
    /// watermark asks next time for the rows whose <see cref="IVersioned.Version"/> is at or above it, and so misses
    /// no change, also of transactions that were still running when it read (it may get some rows twice).
    /// </summary>
    public async Task<long> GetChangeWatermarkAsync(CancellationToken cancellationToken = default)
        => await Database
            .SqlQueryRaw<long>("SELECT pg_snapshot_xmin(pg_current_snapshot())::text::bigint AS \"Value\"")
            .SingleAsync(cancellationToken);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        foreach (var enumType in typeof(ShelfDbContext).Assembly.GetTypes().Where(x => x is { IsEnum: true, IsPublic: true }))
        {
            configurationBuilder.Properties(enumType).HaveConversion(typeof(SnakeCaseEnumConverter<>).MakeGenericType(enumType));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShelfDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IVersioned).IsAssignableFrom(entityType.ClrType))
            {
                var version = entityType.FindProperty(nameof(IVersioned.Version))!;
                version.ValueGenerated = ValueGenerated.OnAddOrUpdate;
                version.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
                version.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
            }

            if (typeof(ITombstoned).IsAssignableFrom(entityType.ClrType))
            {
                var row = Expression.Parameter(entityType.ClrType, "x");
                entityType.SetQueryFilter(Expression.Lambda(
                    Expression.Equal(
                        Expression.Property(row, nameof(ITombstoned.DeletedAt)),
                        Expression.Constant(null, typeof(DateTimeOffset?))),
                    row));
            }
        }
    }
}
