using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Areas.Me.Services;

/// <summary>
/// Leaving: wiping the profile (it stays, empty) and deleting it (step one of <see cref="Account.DeletedAt"/>; the purge
/// removes the row later). Each in one transaction, logged in the audit log.
/// <para>
/// Both: the person leaves every playlist; a playlist they own passes to its longest-standing admin, else member,
/// and one with nobody else in it is marked removed; their marks, keys, notes and recent are marked removed and their
/// choices of variants cleared; the share links into what is removed, their jobs and the analyses of their own files
/// are deleted. Wiping keeps the system playlists (their songs are marked removed), the sign-ins, the
/// sessions, the settings and the music links. Deleting removes those too and clears the name and the picture. What
/// the person added to others' playlists stays there.
/// </para>
/// </summary>
public sealed class ProfileRemoval(ShelfDbContext db, TimeProvider time)
{
    public const string WipedAction = "profile_wiped";
    public const string DeletedAction = "profile_deleted";

    public async Task WipeAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockAsync(accountId, cancellationToken);

        var removed = await ClearAsync(accountId, now, keepSystemPlaylists: true, cancellationToken);
        var systemPlaylists = await db.Playlists.Where(x => x.OwnerId == accountId && x.SystemKind != PlaylistSystemKind.None).Select(x => x.Id).ToListAsync(cancellationToken);
        await db.PlaylistSongs.Where(x => systemPlaylists.Contains(x.PlaylistId))
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.DeletedAt, now), cancellationToken);
        await RemoveWhatNobodyNeedsAsync(accountId, removed.Concat(systemPlaylists).ToList(), cancellationToken);

        Audit(accountId, WipedAction, now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockAsync(accountId, cancellationToken);

        var removed = await ClearAsync(accountId, now, keepSystemPlaylists: false, cancellationToken);
        await RemoveWhatNobodyNeedsAsync(accountId, removed, cancellationToken);

        foreach (var link in await db.MusicLinks.Where(x => x.AccountId == accountId).ToListAsync(cancellationToken))
        {
            db.MusicLinks.Remove(link);
            AudioSubjectPurge.Enqueue(db, link.Id, now);
        }

        await db.AccountLogins.Where(x => x.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
        await db.Sessions.Where(x => x.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
        await db.OAuthGrants.Where(x => x.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
        await db.PersonalKeys.Where(x => x.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
        await db.AccountSettings.Where(x => x.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
        await db.Accounts.Where(x => x.Id == accountId).ExecuteUpdateAsync(x => x
            .SetProperty(a => a.DeletedAt, now)
            .SetProperty(a => a.DisplayName, string.Empty)
            .SetProperty(a => a.PictureUrl, (string?)null)
            .SetProperty(a => a.Locale, (string?)null), cancellationToken);

        Audit(accountId, DeletedAction, now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// The common part; the playlists it marked removed.
    /// </summary>
    private async Task<List<Guid>> ClearAsync(Guid accountId, DateTimeOffset now, bool keepSystemPlaylists, CancellationToken cancellationToken)
    {
        var removed = new List<Guid>();
        // Leaving every live playlist (but, when wiping, one's own system playlists).
        await db.PlaylistMembers
            .Where(m => m.AccountId == accountId && m.Playlist.DeletedAt == null
                        && !(keepSystemPlaylists && m.Playlist.OwnerId == accountId && m.Playlist.SystemKind != PlaylistSystemKind.None))
            .ExecuteUpdateAsync(x => x.SetProperty(m => m.DeletedAt, now), cancellationToken);

        // The playlists one owns pass on, or are removed when nobody else is in them.
        var owned = await db.Playlists
            .Where(p => p.OwnerId == accountId && !(keepSystemPlaylists && p.SystemKind != PlaylistSystemKind.None))
            .ToListAsync(cancellationToken);
        foreach (var playlist in owned)
        {
            var heir = playlist.SystemKind == PlaylistSystemKind.None
                ? await db.PlaylistMembers.Where(m => m.PlaylistId == playlist.Id)
                    .OrderBy(m => m.Role == PlaylistRole.Admin ? 0 : 1).ThenBy(m => m.JoinedAt).ThenBy(m => m.Id)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            if (heir == null)
            {
                playlist.DeletedAt = now;
                removed.Add(playlist.Id);
            }
            else
            {
                heir.Role = PlaylistRole.Owner;
                playlist.OwnerId = heir.AccountId;
            }

            playlist.UpdatedAt = now;
        }

        // the playlists followed are followed no more
        await db.PlaylistFollows.Where(x => x.AccountId == accountId).ExecuteUpdateAsync(x => x.SetProperty(f => f.DeletedAt, now), cancellationToken);
        await db.Marks.Where(x => x.AccountId == accountId).ExecuteUpdateAsync(x => x.SetProperty(m => m.DeletedAt, now), cancellationToken);
        await db.Transpositions.Where(x => x.AccountId == accountId).ExecuteUpdateAsync(x => x.SetProperty(t => t.DeletedAt, now), cancellationToken);
        await db.Notes.Where(x => x.AccountId == accountId).ExecuteUpdateAsync(x => x.SetProperty(n => n.DeletedAt, now), cancellationToken);
        await db.Recent.Where(x => x.AccountId == accountId).ExecuteUpdateAsync(x => x.SetProperty(r => r.DeletedAt, now), cancellationToken);
        await db.PlaylistSongPreferences.Where(x => x.AccountId == accountId && (x.MainItemId != null || x.RecordingItemId != null))
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.MainItemId, (Guid?)null).SetProperty(p => p.RecordingItemId, (Guid?)null), cancellationToken);
        return removed;
    }

    /// <summary>
    /// What nothing needs any more, removed at once rather than by the purge: the share links into the playlists
    /// removed or emptied now (others' links into playlists that stay are kept) and the person's links that carry their
    /// access, the person's jobs, and the analyses of the person's own files.
    /// </summary>
    private async Task RemoveWhatNobodyNeedsAsync(Guid accountId, List<Guid> playlists, CancellationToken cancellationToken)
    {
        await db.ShareLinks
            .Where(x => (x.PlaylistId != null && playlists.Contains(x.PlaylistId.Value)) || (x.SongId != null && playlists.Contains(x.Song!.PlaylistId))
                        || (x.CarriedKeys != null && x.CreatedById == accountId))
            .ExecuteDeleteAsync(cancellationToken);
        await db.Jobs.Where(x => x.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
        await HarmonyDB.Shelf.Api.Areas.Analyses.Services.AnalysisStore.RemoveOwnedAsync(db, accountId, time.GetUtcNow(), cancellationToken);
    }

    /// <summary>
    /// The account's row locked for the transaction: a merge or another removal of it waits.
    /// </summary>
    private async Task LockAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.FromSql($"SELECT * FROM accounts WHERE id = {accountId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (account == null || account.DeletedAt != null)
            throw ShelfException.Conflict();
    }

    private void Audit(Guid accountId, string action, DateTimeOffset now)
        => db.AuditLog.Add(new() { At = now, AccountId = accountId, Actor = $"account:{accountId}", Action = action });
}
