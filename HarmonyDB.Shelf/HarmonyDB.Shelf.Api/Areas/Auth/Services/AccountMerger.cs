using System.Text.Json;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Clients.OwnChords;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Areas.Auth.Services;

/// <summary>
/// Merges a second account into the first, in one transaction, logged in the audit log:
/// <list type="bullet">
/// <item>everything of the second moves to the first: sign-ins, playlists (owned, and memberships), marks, keys, notes,
/// recent, choices of variants, what the second added or made (songs, variants, links, invitations, imports, analyses,
/// jobs);</item>
/// <item>a conflict keeps the first's: the second's row is marked removed and stays with the second (both marked the
/// same chords with the same mark; both have a key, a note, a recent row, a choice for the same thing; settings). Where
/// both are members of one playlist, the first's membership stays with the higher of the two roles; where both have
/// a system playlist of one kind, the second's songs move into the first's;</item>
/// <item>permissions are united;</item>
/// <item>a music link of the second is taken only where the first has none for that service; otherwise it is deleted
/// and what the music service keeps under it is purged;</item>
/// <item>the own chords the second wrote become the first's in their store, after the commit (the outbox);</item>
/// <item>the second's sessions, MCP grants and personal keys are revoked;</item>
/// <item>the second account is left deleted (step one of <see cref="Account.DeletedAt"/>), empty; the purge removes it.</item>
/// </list>
/// Rows already marked removed stay where they are: they are history and go with the purge.
/// </summary>
public sealed class AccountMerger(ShelfDbContext db, TimeProvider time)
{
    public const string AuditAction = "accounts_merged";

    public async Task MergeAsync(Guid first, Guid second, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var moved = new Dictionary<string, int>();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Both rows locked, in one order: a merge or a deletion of either waits for this one.
        var ids = new[] { first, second }.Order().ToArray();
        var accounts = await db.Accounts.FromSql($"SELECT * FROM accounts WHERE id = ANY({ids}) ORDER BY id FOR UPDATE")
            .AsNoTracking().ToListAsync(cancellationToken);
        if (accounts.Count != 2 || accounts.Any(x => x.DeletedAt != null || x.SystemName != null))
            throw ShelfException.Conflict();

        // Sign-ins and permissions.
        moved["logins"] = await db.AccountLogins.Where(x => x.AccountId == second)
            .ExecuteUpdateAsync(x => x.SetProperty(l => l.AccountId, first), cancellationToken);

        var permissions = await db.AccountPermissions
            .Where(p => p.AccountId == second && !db.AccountPermissions.Any(f => f.AccountId == first && f.Permission == p.Permission))
            .AsNoTracking().ToListAsync(cancellationToken);
        db.AccountPermissions.AddRange(permissions.Select(x => new AccountPermission
        {
            AccountId = first,
            Permission = x.Permission,
            GrantedAt = x.GrantedAt,
            GrantedBy = x.GrantedBy,
        }));
        moved["permissions"] = permissions.Count;

        // Settings: the first's, or the second's when the first has none.
        var secondSettings = await db.AccountSettings.SingleOrDefaultAsync(x => x.AccountId == second, cancellationToken);
        if (secondSettings != null)
        {
            if (!await db.AccountSettings.AnyAsync(x => x.AccountId == first, cancellationToken))
            {
                db.AccountSettings.Add(new()
                {
                    AccountId = first,
                    ChordsScale = secondSettings.ChordsScale,
                    InterfaceScale = secondSettings.InterfaceScale,
                    LessMode = secondSettings.LessMode,
                    Accidentals = secondSettings.Accidentals,
                    ShowDegrees = secondSettings.ShowDegrees,
                    ChordSuperscripts = secondSettings.ChordSuperscripts,
                    Theme = secondSettings.Theme,
                    UpdatedAt = now,
                });
            }

            db.AccountSettings.Remove(secondSettings);
        }

        // Music links: the second's only where the first has none.
        var firstServices = await db.MusicLinks.Where(x => x.AccountId == first).Select(x => x.Service).ToListAsync(cancellationToken);
        foreach (var link in await db.MusicLinks.Where(x => x.AccountId == second).ToListAsync(cancellationToken))
        {
            if (firstServices.Contains(link.Service))
            {
                db.MusicLinks.Remove(link);
                AudioSubjectPurge.Enqueue(db, link.Id, now);
            }
            else
            {
                link.AccountId = first;
                moved[$"music_link_{link.Service.ToString().ToLowerInvariant()}"] = 1;
            }
        }

        // The own chords the second wrote, kept by their store: moved there after this commits.
        OwnChordsAuthorMove.Enqueue(db, second, first, now);

        // Revoked.
        await db.Sessions.Where(x => x.AccountId == second).ExecuteDeleteAsync(cancellationToken);
        await db.OAuthGrants.Where(x => x.AccountId == second).ExecuteDeleteAsync(cancellationToken);
        await db.PersonalKeys.Where(x => x.AccountId == second).ExecuteDeleteAsync(cancellationToken);

        // Marks, keys, notes, recent: a conflict keeps the first's.
        await db.Marks
            .Where(m => m.AccountId == second && db.Marks.Any(f => f.AccountId == first && f.ChordsId == m.ChordsId && f.Kind == m.Kind))
            .ExecuteUpdateAsync(x => x.SetProperty(m => m.DeletedAt, now), cancellationToken);
        moved["marks"] = await db.Marks.Where(x => x.AccountId == second)
            .ExecuteUpdateAsync(x => x.SetProperty(m => m.AccountId, first), cancellationToken);

        await db.Transpositions
            .Where(t => t.AccountId == second && db.Transpositions.Any(f => f.AccountId == first && f.ChordsId == t.ChordsId && f.PlaylistId == t.PlaylistId))
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.DeletedAt, now), cancellationToken);
        moved["keys"] = await db.Transpositions.Where(x => x.AccountId == second)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.AccountId, first), cancellationToken);

        await db.Notes
            .Where(n => n.AccountId == second && db.Notes.Any(f => f.AccountId == first
                && ((n.ChordsId != null && f.ChordsId == n.ChordsId) || (n.SongId != null && f.SongId == n.SongId))))
            .ExecuteUpdateAsync(x => x.SetProperty(n => n.DeletedAt, now), cancellationToken);
        moved["notes"] = await db.Notes.Where(x => x.AccountId == second)
            .ExecuteUpdateAsync(x => x.SetProperty(n => n.AccountId, first), cancellationToken);

        await db.Recent
            .Where(r => r.AccountId == second && db.Recent.Any(f => f.AccountId == first && f.ChordsId == r.ChordsId && f.SongId == r.SongId))
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.DeletedAt, now), cancellationToken);
        moved["recent"] = await db.Recent.Where(x => x.AccountId == second)
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.AccountId, first), cancellationToken);

        moved["choices"] = await db.PlaylistSongPreferences.IgnoreQueryFilters()
            .Where(p => p.AccountId == second && !db.PlaylistSongPreferences.IgnoreQueryFilters().Any(f => f.AccountId == first && f.SongId == p.SongId))
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.AccountId, first), cancellationToken);

        // System playlists of a kind both have: the second's songs move into the first's.
        var firstSystem = await db.Playlists.Where(x => x.OwnerId == first && x.SystemKind != PlaylistSystemKind.None)
            .ToDictionaryAsync(x => x.SystemKind, cancellationToken);
        foreach (var playlist in await db.Playlists.Where(x => x.OwnerId == second && x.SystemKind != PlaylistSystemKind.None).ToListAsync(cancellationToken))
        {
            if (!firstSystem.TryGetValue(playlist.SystemKind, out var target))
                continue;

            var offset = await db.PlaylistSongs.IgnoreQueryFilters().Where(x => x.PlaylistId == target.Id)
                .MaxAsync(x => (double?)x.Position, cancellationToken) ?? 0;
            await db.PlaylistSongs.IgnoreQueryFilters().Where(x => x.PlaylistId == playlist.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(s => s.PlaylistId, target.Id).SetProperty(s => s.Position, s => s.Position + offset + 1), cancellationToken);
            await db.PlaylistMembers.Where(x => x.PlaylistId == playlist.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(m => m.DeletedAt, now), cancellationToken);
            playlist.DeletedAt = now;
            playlist.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        // Memberships of live playlists where both are members: the first's stays, with the higher role.
        var both = await db.PlaylistMembers
            .Where(s => s.AccountId == second && s.Playlist.DeletedAt == null)
            .Join(db.PlaylistMembers.Where(f => f.AccountId == first), s => s.PlaylistId, f => f.PlaylistId, (s, f) => new { Second = s, First = f })
            .ToListAsync(cancellationToken);
        foreach (var pair in both)
            pair.Second.DeletedAt = now;

        await db.SaveChangesAsync(cancellationToken);

        foreach (var pair in both.Where(x => Rank(x.Second.Role) > Rank(x.First.Role)))
            pair.First.Role = pair.Second.Role;

        await db.SaveChangesAsync(cancellationToken);

        moved["memberships"] = await db.PlaylistMembers.Where(x => x.AccountId == second && x.Playlist.DeletedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(m => m.AccountId, first), cancellationToken);
        moved["playlists"] = await db.Playlists.Where(x => x.OwnerId == second)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.OwnerId, first).SetProperty(p => p.UpdatedAt, now), cancellationToken);

        // What the second added or made.
        await db.Playlists.IgnoreQueryFilters().Where(x => x.ImportedById == second)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.ImportedById, first), cancellationToken);
        await db.PlaylistSongs.IgnoreQueryFilters().Where(x => x.AddedById == second)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.AddedById, first), cancellationToken);
        await db.PlaylistSongItems.IgnoreQueryFilters().Where(x => x.AddedById == second)
            .ExecuteUpdateAsync(x => x.SetProperty(i => i.AddedById, first), cancellationToken);
        await db.ShareLinks.Where(x => x.CreatedById == second)
            .ExecuteUpdateAsync(x => x.SetProperty(l => l.CreatedById, first), cancellationToken);
        await db.PlaylistInvitations.IgnoreQueryFilters().Where(x => x.CreatedById == second)
            .ExecuteUpdateAsync(x => x.SetProperty(i => i.CreatedById, first), cancellationToken);
        await db.Analyses.Where(x => x.OwnerId == second)
            .ExecuteUpdateAsync(x => x.SetProperty(a => a.OwnerId, first), cancellationToken);
        await db.Jobs.Where(x => x.AccountId == second)
            .ExecuteUpdateAsync(x => x.SetProperty(j => j.AccountId, first), cancellationToken);

        // The second is left deleted and empty.
        await db.Accounts.Where(x => x.Id == second).ExecuteUpdateAsync(x => x
            .SetProperty(a => a.DeletedAt, now)
            .SetProperty(a => a.DisplayName, string.Empty)
            .SetProperty(a => a.PictureUrl, (string?)null)
            .SetProperty(a => a.Locale, (string?)null), cancellationToken);

        db.AuditLog.Add(new()
        {
            At = now,
            AccountId = first,
            Actor = $"account:{first}",
            Action = AuditAction,
            Data = JsonSerializer.Serialize(new { merged = second, moved }),
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static int Rank(PlaylistRole role) => role switch
    {
        PlaylistRole.Owner => 2,
        PlaylistRole.Admin => 1,
        _ => 0,
    };
}
