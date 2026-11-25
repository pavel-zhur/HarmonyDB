using System.Security.Cryptography;
using HarmonyDB.Shelf.Api.Areas.Sync.Services;
using HarmonyDB.Shelf.Api.Common;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Model.Jobs;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using M = HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Api.Areas.Playlists.Services;

/// <summary>
/// Playlists: the person's list, the public ones, a playlist's page; creating, settings, deleting (marked removed);
/// members, their roles, leaving, passing the ownership on; "show in my library"; removing all one's marks from a
/// playlist; invitations — the only way into the members.
/// </summary>
public sealed class PlaylistService(ShelfDbContext db, PlaylistGate gate, Views views, UndoTokens undo, SystemPlaylists systemPlaylists, SongService songs, JobQueue jobs, IOptionsMonitor<PlaylistsOptions> options, TimeProvider time)
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MaxInvitationDays = 365;
    public const int MaxInvitationUses = 10000;
    public const string SyncJobKind = "playlist_sync";

    /// <summary>
    /// The person's playlists (created by them and shared with them), their system playlists first, then by when the
    /// person joined; then those they follow.
    /// </summary>
    public async Task<List<M.PlaylistSummary>> ListMineAsync(Guid me, CancellationToken cancellationToken)
    {
        await systemPlaylists.EnsureAllAsync(me, cancellationToken);
        var playlists = await db.PlaylistMembers.Where(x => x.AccountId == me && x.Playlist.DeletedAt == null)
            .OrderBy(x => x.Playlist.SystemKind == PlaylistSystemKind.None ? 1 : 0).ThenBy(x => x.Playlist.SystemKind).ThenBy(x => x.JoinedAt)
            .Select(x => x.Playlist)
            .ToListAsync(cancellationToken);
        // then those followed (not while private)
        playlists.AddRange(await db.PlaylistFollows.Where(x => x.AccountId == me && x.Playlist.DeletedAt == null && x.Playlist.Access != PlaylistAccess.Private
                                                               && !x.Playlist.Members.Any(m => m.AccountId == me))
            .OrderBy(x => x.FollowedAt).Select(x => x.Playlist).ToListAsync(cancellationToken));
        return await views.SummariesAsync(me, playlists, cancellationToken);
    }

    /// <summary>
    /// The public playlists, the most recently changed first; with a text, those whose title has it.
    /// </summary>
    public async Task<List<M.PlaylistSummary>> ListPublicAsync(Guid? viewer, string? q, int limit, CancellationToken cancellationToken)
    {
        var query = db.Playlists.Where(x => x.Access == PlaylistAccess.Public);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => EF.Functions.ILike(x.Title, "%" + EscapeLike(q.Trim()) + "%"));

        var playlists = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Take(Math.Clamp(limit, 1, 200)).ToListAsync(cancellationToken);
        return await views.SummariesAsync(viewer, playlists, cancellationToken);
    }

    public async Task<M.PlaylistDetails> GetAsync(Guid? viewer, Guid playlistId, string? share, CancellationToken cancellationToken)
    {
        var view = await gate.ReadAsync(viewer, playlistId, share, cancellationToken);
        return await DetailsAsync(viewer, view, cancellationToken);
    }

    public async Task<M.Changed<M.PlaylistDetails>> CreateAsync(Guid me, M.CreatePlaylistRequest request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            OwnerId = me,
            Title = Title(request.Title),
            Description = Description(request.Description),
            Access = Views.Map<PlaylistAccess>(request.Access),
            AddPolicy = Views.Map<PlaylistAddPolicy>(request.AddPolicy),
            SearchFirst = request.SearchFirst is { } first ? Views.Map<MusicService>(first) : null,
            SystemKind = PlaylistSystemKind.None,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var membership = new PlaylistMember { Id = Guid.NewGuid(), PlaylistId = playlist.Id, AccountId = me, Role = PlaylistRole.Owner, JoinedAt = now };
        db.Playlists.Add(playlist);
        db.PlaylistMembers.Add(membership);
        var token = await undo.SaveAsync(me, cancellationToken);
        return new() { Value = await DetailsAsync(me, new(playlist, membership), cancellationToken), Undo = token };
    }

    public async Task<M.Changed<M.PlaylistDetails>> UpdateAsync(Guid me, Guid playlistId, M.UpdatePlaylistRequest request, CancellationToken cancellationToken)
    {
        var view = await gate.ForChangeAsync(me, playlistId, cancellationToken);
        view.RequireAdmin();
        var playlist = view.Playlist;
        if (request.Title != null)
        {
            view.RequireNotSystem();
            playlist.Title = Title(request.Title);
        }

        if (request.Description != null)
            playlist.Description = Description(request.Description);
        if (request.Access is { } access)
        {
            // the system playlists are private: their access is fixed
            view.RequireNotSystem();
            playlist.Access = Views.Map<PlaylistAccess>(access);
        }
        if (request.AddPolicy is { } policy)
            playlist.AddPolicy = Views.Map<PlaylistAddPolicy>(policy);
        if (request.ClearSearchFirst)
            playlist.SearchFirst = null;
        else if (request.SearchFirst is { } first)
            playlist.SearchFirst = Views.Map<MusicService>(first);

        return await SaveDetailsAsync(me, view, cancellationToken);
    }

    /// <summary>
    /// Marks the playlist removed (with its songs, members and links as they are, hidden with it); the owner's, not a
    /// system one.
    /// </summary>
    public async Task<M.UndoOnly> DeleteAsync(Guid me, Guid playlistId, CancellationToken cancellationToken)
    {
        var view = await gate.ForChangeAsync(me, playlistId, cancellationToken);
        view.RequireOwner();
        view.RequireNotSystem();
        view.Playlist.DeletedAt = time.GetUtcNow();
        view.Playlist.UpdatedAt = time.GetUtcNow();
        return new() { Undo = await undo.SaveAsync(me, cancellationToken) };
    }

    /// <summary>
    /// "Show in my library", for a member or a follower.
    /// </summary>
    public async Task<M.Changed<M.PlaylistDetails>> ShowInLibraryAsync(Guid me, Guid playlistId, bool show, CancellationToken cancellationToken)
    {
        var view = await gate.ReadAsync(me, playlistId, null, cancellationToken);
        if (view.Membership != null)
            view.Membership.ShowInLibrary = show;
        else if (view.IsFollower)
            view.Follow!.ShowInLibrary = show;
        else
            throw ShelfException.Forbidden(M.PlaylistErrorCodes.MembersOnly);

        var token = await undo.SaveAsync(me, cancellationToken);
        return new() { Value = await DetailsAsync(me, view, cancellationToken), Undo = token };
    }

    /// <summary>
    /// Follows a playlist the person may look at (public, or of link access by its link), to keep it: without being a
    /// member. A very large one starts out of the library (the answer says so). Following again changes nothing.
    /// </summary>
    public async Task<M.Changed<M.JoinedPlaylist>> FollowAsync(Guid me, Guid playlistId, string? share, CancellationToken cancellationToken)
    {
        var view = await gate.ReadAsync(me, playlistId, share, cancellationToken);
        view.RequireNotSystem();
        if (view.IsMember)
            throw ShelfException.Conflict(M.PlaylistErrorCodes.AlreadyMember);

        string? token = null;
        var large = false;
        if (view.Follow == null)
        {
            large = await db.PlaylistSongs.CountAsync(x => x.PlaylistId == playlistId, cancellationToken) > options.CurrentValue.LibraryShowLimit;
            db.PlaylistFollows.Add(new()
            {
                Id = Guid.NewGuid(),
                PlaylistId = playlistId,
                AccountId = me,
                // a very large playlist does not flood the library: it starts out of it, and the person is told
                ShowInLibrary = !large,
                FollowedAt = time.GetUtcNow(),
            });
            try
            {
                token = await undo.SaveAsync(me, cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // followed at the same moment from elsewhere: it is followed
                db.ChangeTracker.Clear();
            }

            view = await gate.ReadAsync(me, playlistId, share, cancellationToken);
        }

        var details = await DetailsAsync(me, view, cancellationToken);
        return new() { Value = new M.JoinedPlaylist(details) { KeptOutOfLibrary = large }, Undo = token };
    }

    public async Task<M.UndoOnly> UnfollowAsync(Guid me, Guid playlistId, CancellationToken cancellationToken)
    {
        var follow = await db.PlaylistFollows.SingleOrDefaultAsync(x => x.PlaylistId == playlistId && x.AccountId == me, cancellationToken)
                     ?? throw ShelfException.NotFound(M.PlaylistErrorCodes.NoSuchPlaylist);
        follow.DeletedAt = time.GetUtcNow();
        return new() { Undo = await undo.SaveAsync(me, cancellationToken) };
    }

    /// <summary>
    /// Leaves the playlist. The owner passes it on first (or deletes it); a system playlist is not left.
    /// </summary>
    public async Task<M.UndoOnly> LeaveAsync(Guid me, Guid playlistId, CancellationToken cancellationToken)
    {
        var view = await gate.ForChangeAsync(me, playlistId, cancellationToken);
        view.RequireNotSystem();
        if (view.IsOwner)
            throw ShelfException.Conflict(M.PlaylistErrorCodes.OwnerCannotLeave);

        var now = time.GetUtcNow();
        view.Membership!.DeletedAt = now;
        await RemoveKeysAndNotesAsync(me, playlistId, now, cancellationToken);
        return new() { Undo = await undo.SaveAsync(me, cancellationToken) };
    }

    /// <summary>
    /// Clears what is the person's in the playlist: their "here" keys and their notes in it, and, when asked, what they
    /// added to it (their variants; a song left with none is removed). Marks are not touched: they are the person's on
    /// chords, everywhere. One undo brings it all back.
    /// </summary>
    public async Task<M.UndoOnly> ClearMineAsync(Guid me, Guid playlistId, bool added, CancellationToken cancellationToken)
    {
        await gate.ForChangeAsync(me, playlistId, cancellationToken);
        var now = time.GetUtcNow();
        await RemoveKeysAndNotesAsync(me, playlistId, now, cancellationToken);
        if (added)
        {
            var items = await db.PlaylistSongItems.Where(x => x.AddedById == me && x.Song.PlaylistId == playlistId && x.Song.DeletedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var item in items)
                item.DeletedAt = now;

            // a song left with nothing of its own goes (see SongService.Keeps)
            var touched = items.Select(x => x.SongId).Distinct().ToList();
            var remaining = (await db.PlaylistSongItems.Where(i => touched.Contains(i.SongId) && i.AddedById != me).ToListAsync(cancellationToken)).ToLookup(i => i.SongId);
            foreach (var song in await db.PlaylistSongs.Where(s => touched.Contains(s.Id)).ToListAsync(cancellationToken))
            {
                if (!SongService.Keeps(remaining[song.Id]))
                    await songs.RemoveSongAsync(song, now, cancellationToken);
            }
        }

        return new() { Undo = await undo.SaveAsync(me, cancellationToken) };
    }

    /// <summary>
    /// Makes a member an admin or a member again: the owner, of anyone; an admin, of members only.
    /// </summary>
    public async Task<M.Changed<M.PlaylistDetails>> SetRoleAsync(Guid me, Guid playlistId, Guid accountId, M.PlaylistRole role, CancellationToken cancellationToken)
    {
        if (role == M.PlaylistRole.Owner)
            throw ShelfException.BadRequest();

        var view = await gate.ForChangeAsync(me, playlistId, cancellationToken);
        var member = await OtherMemberAsync(view, accountId, cancellationToken);
        member.Role = Views.Map<PlaylistRole>(role);
        return await SaveDetailsAsync(me, view, cancellationToken);
    }

    /// <summary>
    /// Removes a member: the owner, anyone; an admin, members only.
    /// </summary>
    public async Task<M.Changed<M.PlaylistDetails>> RemoveMemberAsync(Guid me, Guid playlistId, Guid accountId, CancellationToken cancellationToken)
    {
        var view = await gate.ForChangeAsync(me, playlistId, cancellationToken);
        var member = await OtherMemberAsync(view, accountId, cancellationToken);
        var now = time.GetUtcNow();
        member.DeletedAt = now;
        await RemoveKeysAndNotesAsync(accountId, playlistId, now, cancellationToken);
        return await SaveDetailsAsync(me, view, cancellationToken);
    }

    /// <summary>
    /// Passes the playlist to another member; the owner becomes an admin.
    /// </summary>
    public async Task<M.Changed<M.PlaylistDetails>> PassOwnershipAsync(Guid me, Guid playlistId, Guid accountId, CancellationToken cancellationToken)
    {
        var view = await gate.ForChangeAsync(me, playlistId, cancellationToken);
        view.RequireOwner();
        view.RequireNotSystem();
        var heir = await db.PlaylistMembers.SingleOrDefaultAsync(x => x.PlaylistId == playlistId && x.AccountId == accountId && x.AccountId != me, cancellationToken)
                   ?? throw ShelfException.NotFound(M.PlaylistErrorCodes.NoSuchMember);

        // the one owner's index: the old owner steps down first, in the same transaction
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        view.Membership!.Role = PlaylistRole.Admin;
        var steps = UndoTokens.Capture(db);
        await db.SaveChangesAsync(cancellationToken);
        heir.Role = PlaylistRole.Owner;
        view.Playlist.OwnerId = accountId;
        view.Playlist.UpdatedAt = time.GetUtcNow();
        // undone in the reverse order: the heir steps down before the owner comes back
        steps.InsertRange(0, UndoTokens.Capture(db));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new() { Value = await DetailsAsync(me, view, cancellationToken), Undo = undo.Issue(me, steps) };
    }

    public async Task<M.Changed<M.InvitationItem>> CreateInvitationAsync(Guid me, Guid playlistId, M.CreateInvitationRequest request, CancellationToken cancellationToken)
    {
        if (request.Role == M.PlaylistRole.Owner
            || request.ExpiresInDays is < 1 or > MaxInvitationDays
            || request.MaxUses is < 1 or > MaxInvitationUses)
            throw ShelfException.BadRequest();

        var view = await gate.ForChangeAsync(me, playlistId, cancellationToken);
        view.RequireAdmin();
        view.RequireNotSystem();
        var now = time.GetUtcNow();
        var invitation = new PlaylistInvitation
        {
            Token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24)),
            PlaylistId = playlistId,
            Role = Views.Map<PlaylistRole>(request.Role),
            CreatedById = me,
            CreatedAt = now,
            ExpiresAt = now.AddDays(request.ExpiresInDays),
            MaxUses = request.MaxUses,
        };
        db.PlaylistInvitations.Add(invitation);
        var token = await undo.SaveAsync(me, cancellationToken);
        var people = await views.PeopleAsync([me], cancellationToken);
        return new() { Value = Invitation(invitation, people), Undo = token };
    }

    public async Task<M.UndoOnly> RevokeInvitationAsync(Guid me, Guid playlistId, string token, CancellationToken cancellationToken)
    {
        var view = await gate.ForChangeAsync(me, playlistId, cancellationToken);
        view.RequireAdmin();
        var invitation = await db.PlaylistInvitations.SingleOrDefaultAsync(x => x.Token == token && x.PlaylistId == playlistId, cancellationToken)
                         ?? throw ShelfException.NotFound(M.PlaylistErrorCodes.NoSuchInvitation);
        invitation.RevokedAt ??= time.GetUtcNow();
        return new() { Undo = await undo.SaveAsync(me, cancellationToken) };
    }

    /// <summary>
    /// What an invitation's link shows; for guests too (they sign in, then accept).
    /// </summary>
    public async Task<M.InvitationView> GetInvitationAsync(Guid? viewer, string token, CancellationToken cancellationToken)
    {
        var invitation = await db.PlaylistInvitations.Include(x => x.Playlist).SingleOrDefaultAsync(x => x.Token == token, cancellationToken)
                         ?? throw ShelfException.NotFound(M.PlaylistErrorCodes.NoSuchInvitation);
        // one no longer valid tells nothing of the playlist
        if (!IsValid(invitation))
            throw new ShelfException(StatusCodes.Status410Gone, M.PlaylistErrorCodes.InvitationInvalid);

        var people = await views.PeopleAsync(new[] { invitation.Playlist.OwnerId, invitation.CreatedById ?? Guid.Empty }, cancellationToken);
        var member = viewer != null && await db.PlaylistMembers.AnyAsync(x => x.PlaylistId == invitation.PlaylistId && x.AccountId == viewer, cancellationToken);
        return new()
        {
            Token = invitation.Token,
            PlaylistId = invitation.PlaylistId,
            PlaylistTitle = invitation.Playlist.Title,
            SystemKind = Views.Map<M.PlaylistSystemKind>(invitation.Playlist.SystemKind),
            Owner = people[invitation.Playlist.OwnerId],
            InvitedBy = invitation.CreatedById is { } by ? people.GetValueOrDefault(by) : null,
            Role = Views.Map<M.PlaylistRole>(invitation.Role),
            ExpiresAt = invitation.ExpiresAt,
            AlreadyMember = member,
        };
    }

    /// <summary>
    /// Joins the playlist by the invitation, with its role.
    /// </summary>
    public async Task<M.Changed<M.JoinedPlaylist>> AcceptInvitationAsync(Guid me, string token, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var invitation = await db.PlaylistInvitations.Include(x => x.Playlist).SingleOrDefaultAsync(x => x.Token == token, cancellationToken)
                         ?? throw ShelfException.NotFound(M.PlaylistErrorCodes.NoSuchInvitation);
        if (await db.PlaylistMembers.AnyAsync(x => x.PlaylistId == invitation.PlaylistId && x.AccountId == me, cancellationToken))
            throw ShelfException.Conflict(M.PlaylistErrorCodes.AlreadyMember);

        // one use taken, if one is left: at once for everyone accepting together
        var now = time.GetUtcNow();
        var taken = await db.PlaylistInvitations
            .Where(x => x.Token == token && x.RevokedAt == null && x.ExpiresAt > now && (x.MaxUses == null || x.Uses < x.MaxUses))
            .ExecuteUpdateAsync(x => x.SetProperty(i => i.Uses, i => i.Uses + 1), cancellationToken);
        if (taken == 0)
            throw new ShelfException(StatusCodes.Status410Gone, M.PlaylistErrorCodes.InvitationInvalid);

        var large = await db.PlaylistSongs.CountAsync(x => x.PlaylistId == invitation.PlaylistId, cancellationToken) > options.CurrentValue.LibraryShowLimit;
        var membership = new PlaylistMember
        {
            Id = Guid.NewGuid(),
            PlaylistId = invitation.PlaylistId,
            AccountId = me,
            Role = invitation.Role,
            JoinedAt = now,
            // a very large playlist does not flood the library: it starts out of it, and the person is told
            ShowInLibrary = !large,
        };
        db.PlaylistMembers.Add(membership);
        // a member follows no more
        foreach (var follow in await db.PlaylistFollows.Where(x => x.PlaylistId == invitation.PlaylistId && x.AccountId == me).ToListAsync(cancellationToken))
            follow.DeletedAt = now;
        string? undoToken;
        try
        {
            undoToken = await undo.SaveAsync(me, cancellationToken, UndoStep.ReturnInvitationUse(token));
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw ShelfException.Conflict(M.PlaylistErrorCodes.AlreadyMember);
        }

        await transaction.CommitAsync(cancellationToken);
        var details = await DetailsAsync(me, new(invitation.Playlist, membership), cancellationToken);
        return new() { Value = new M.JoinedPlaylist(details) { KeptOutOfLibrary = large }, Undo = undoToken };
    }

    /// <summary>
    /// Marks removed the person's "here" keys and notes in the playlist (they go when the person leaves or is removed,
    /// and come back with the undo).
    /// </summary>
    private async Task RemoveKeysAndNotesAsync(Guid accountId, Guid playlistId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var key in await db.Transpositions.Where(x => x.AccountId == accountId && x.PlaylistId == playlistId).ToListAsync(cancellationToken))
            key.DeletedAt = now;
        foreach (var note in await db.Notes.Where(x => x.AccountId == accountId && x.Song!.PlaylistId == playlistId).ToListAsync(cancellationToken))
            note.DeletedAt = now;
    }

    private async Task<M.Changed<M.PlaylistDetails>> SaveDetailsAsync(Guid me, PlaylistView view, CancellationToken cancellationToken)
    {
        view.Playlist.UpdatedAt = time.GetUtcNow();
        var token = await undo.SaveAsync(me, cancellationToken);
        return new() { Value = await DetailsAsync(me, view, cancellationToken), Undo = token };
    }

    /// <summary>
    /// Another member, whom the caller may change: the owner, anyone; an admin, members only.
    /// </summary>
    private async Task<PlaylistMember> OtherMemberAsync(PlaylistView view, Guid accountId, CancellationToken cancellationToken)
    {
        view.RequireAdmin();
        var member = await db.PlaylistMembers.SingleOrDefaultAsync(x => x.PlaylistId == view.Playlist.Id && x.AccountId == accountId, cancellationToken)
                     ?? throw ShelfException.NotFound(M.PlaylistErrorCodes.NoSuchMember);
        if (member.Id == view.Membership!.Id || member.Role == PlaylistRole.Owner || (!view.IsOwner && member.Role == PlaylistRole.Admin))
            throw ShelfException.Forbidden(M.PlaylistErrorCodes.OwnerOnly);

        return member;
    }

    private async Task<M.PlaylistDetails> DetailsAsync(Guid? viewer, PlaylistView view, CancellationToken cancellationToken)
    {
        var playlistId = view.Playlist.Id;
        var members = await db.PlaylistMembers.Where(x => x.PlaylistId == playlistId)
            .OrderBy(x => x.Role == PlaylistRole.Owner ? 0 : x.Role == PlaylistRole.Admin ? 1 : 2).ThenBy(x => x.JoinedAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var accountIds = members.Select(x => x.AccountId).ToList();
        var telegram = (await db.AccountLogins.Where(x => accountIds.Contains(x.AccountId) && x.Provider == "telegram" && x.Username != null)
                .Select(x => new { x.AccountId, x.Username }).ToListAsync(cancellationToken))
            .ToLookup(x => x.AccountId, x => x.Username!);
        var invitations = view.IsAdmin
            ? await db.PlaylistInvitations.Where(x => x.PlaylistId == playlistId && x.RevokedAt == null && x.ExpiresAt > time.GetUtcNow())
                .OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken)
            : [];
        var people = await views.PeopleAsync(accountIds.Concat(invitations.Where(x => x.CreatedById != null).Select(x => x.CreatedById!.Value)), cancellationToken);

        return new()
        {
            Playlist = await views.SummaryAsync(viewer, view.Playlist, cancellationToken),
            Members = members.Select(x => new M.PlaylistMemberItem
            {
                Person = people[x.AccountId],
                Role = Views.Map<M.PlaylistRole>(x.Role),
                JoinedAt = x.JoinedAt,
                Contacts = new() { Telegram = telegram[x.AccountId].ToList() },
            }).ToList(),
            CanAdd = view.CanAdd,
            CanAdminister = view.IsAdmin,
            Invitations = invitations.Select(x => Invitation(x, people)).ToList(),
            Sync = viewer != null && view.Playlist.ImportedById == viewer ? await SyncStateAsync(viewer.Value, playlistId, cancellationToken) : null,
        };
    }

    /// <summary>
    /// The latest sync of the playlist the person started (only its importer starts them).
    /// </summary>
    private async Task<JobInfo?> SyncStateAsync(Guid accountId, Guid playlistId, CancellationToken cancellationToken)
    {
        var subject = playlistId.ToString();
        var latest = await db.Jobs.Where(x => x.Kind == SyncJobKind && x.SubjectId == subject && x.AccountId == accountId)
            .OrderByDescending(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        return latest == null ? null : await jobs.GetAsync(accountId, latest.Value, cancellationToken);
    }

    private static M.InvitationItem Invitation(PlaylistInvitation x, IReadOnlyDictionary<Guid, M.PersonRef> people) => new()
    {
        Token = x.Token,
        Role = Views.Map<M.PlaylistRole>(x.Role),
        CreatedBy = x.CreatedById is { } by ? people.GetValueOrDefault(by) : null,
        CreatedAt = x.CreatedAt,
        ExpiresAt = x.ExpiresAt,
        MaxUses = x.MaxUses,
        Uses = x.Uses,
        RevokedAt = x.RevokedAt,
    };

    private bool IsValid(PlaylistInvitation x)
        => x.RevokedAt == null && x.ExpiresAt > time.GetUtcNow() && (x.MaxUses == null || x.Uses < x.MaxUses);

    public static string Title(string? title)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw ShelfException.BadRequest();
        if (trimmed.Length > MaxTitleLength)
            throw ShelfException.BadRequest(M.PlaylistErrorCodes.TooLong);

        return trimmed;
    }

    private static string? Description(string? description)
    {
        var trimmed = description?.Trim();
        if (trimmed?.Length > MaxDescriptionLength)
            throw ShelfException.BadRequest(M.PlaylistErrorCodes.TooLong);

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static string EscapeLike(string text) => text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
