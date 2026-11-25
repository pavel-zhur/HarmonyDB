using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HarmonyDB.Shelf.Api.Common;

/// <summary>
/// The playlists the app keeps for each person (<see cref="PlaylistSystemKind"/>): made when first needed, one of each
/// kind per person, private, the person their owner. They cannot be deleted, left or passed on; their songs can be
/// removed. Their title is the kind's name; the frontend words it.
/// </summary>
public sealed class SystemPlaylists(ShelfDbContext db, TimeProvider time)
{
    public static readonly IReadOnlyList<PlaylistSystemKind> Kinds =
        [PlaylistSystemKind.ImportedChords, PlaylistSystemKind.OwnChords, PlaylistSystemKind.OwnAnalyses];

    /// <summary>
    /// The person's system playlist of the kind, made if it is not there. In the caller's transaction, if there is one.
    /// </summary>
    public async Task<Playlist> GetOrCreateAsync(Guid accountId, PlaylistSystemKind kind, CancellationToken cancellationToken)
    {
        if (kind == PlaylistSystemKind.None)
            throw new ArgumentOutOfRangeException(nameof(kind));

        var existing = await FindAsync(accountId, kind, cancellationToken);
        if (existing != null)
            return existing;

        var now = time.GetUtcNow();
        var name = TitleOf(kind);
        // one per person and kind: the partial unique index decides between two requests at once
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             WITH p AS (
                 INSERT INTO playlists (id, owner_id, title, access, add_policy, system_kind, created_at, updated_at)
                 VALUES (gen_random_uuid(), {accountId}, {name}, 'private', 'admins', {name}, {now}, {now})
                 ON CONFLICT (owner_id, system_kind) WHERE system_kind <> 'none' AND deleted_at IS NULL DO NOTHING
                 RETURNING id)
             INSERT INTO playlist_members (id, playlist_id, account_id, role, show_in_library, joined_at)
             SELECT gen_random_uuid(), p.id, {accountId}, 'owner', TRUE, {now} FROM p
             """,
            cancellationToken);

        return (await FindAsync(accountId, kind, cancellationToken))!;
    }

    /// <summary>
    /// Makes those of the person's system playlists that are not there.
    /// </summary>
    public async Task EnsureAllAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var have = await db.Playlists.Where(x => x.OwnerId == accountId && x.SystemKind != PlaylistSystemKind.None)
            .Select(x => x.SystemKind).ToListAsync(cancellationToken);
        foreach (var kind in Kinds.Except(have))
            await GetOrCreateAsync(accountId, kind, cancellationToken);
    }

    /// <summary>
    /// The stored title of a system playlist: the kind as the database keeps it.
    /// </summary>
    public static string TitleOf(PlaylistSystemKind kind) => kind switch
    {
        PlaylistSystemKind.ImportedChords => "imported_chords",
        PlaylistSystemKind.OwnChords => "own_chords",
        PlaylistSystemKind.OwnAnalyses => "own_analyses",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private Task<Playlist?> FindAsync(Guid accountId, PlaylistSystemKind kind, CancellationToken cancellationToken)
        => db.Playlists.SingleOrDefaultAsync(x => x.OwnerId == accountId && x.SystemKind == kind, cancellationToken);
}

public static class SystemPlaylistsExtensions
{
    /// <summary>
    /// Adds <see cref="SystemPlaylists"/> and <see cref="ChordsAnnotations"/>; safe to call from every area that needs
    /// them.
    /// </summary>
    public static IServiceCollection AddShelfCommon(this IServiceCollection services)
    {
        services.TryAddScoped<SystemPlaylists>();
        services.TryAddScoped<ChordsAnnotations>();
        return services;
    }
}
