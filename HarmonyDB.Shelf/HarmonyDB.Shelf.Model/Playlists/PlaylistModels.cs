using System.Diagnostics.CodeAnalysis;
using HarmonyDB.Shelf.Model.Jobs;
using HarmonyDB.Shelf.Model.Me;

namespace HarmonyDB.Shelf.Model.Playlists;

public enum PlaylistAccess
{
    /// <summary>Its members only.</summary>
    Private,

    /// <summary>Anyone holding its link may look.</summary>
    Link,

    /// <summary>Open to all, and listed.</summary>
    Public,
}

public enum PlaylistAddPolicy
{
    AllMembers,
    Admins,
}

public enum PlaylistRole
{
    Owner,
    Admin,
    Member,
}

/// <summary>
/// The playlists the app keeps for each person, which cannot be deleted (their songs can). The frontend words their
/// titles by the kind.
/// </summary>
public enum PlaylistSystemKind
{
    None,
    ImportedChords,
    OwnChords,
    OwnAnalyses,
}

/// <summary>
/// A person as others see them. <see cref="Gone"/>: the profile was deleted (no name, no picture).
/// </summary>
public record PersonRef
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? PictureUrl { get; init; }

    public bool Gone { get; init; }
}

public record PlaylistOrigin
{
    public required MusicService Service { get; init; }

    /// <summary>
    /// The playlist's id on its service.
    /// </summary>
    public required string PlaylistId { get; init; }

    /// <summary>
    /// The link it was imported by; null when it was imported by its id.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Whether the caller imported it: only they see the sync and may press it.
    /// </summary>
    public required bool CanSync { get; init; }

    public DateTimeOffset? LastSyncedAt { get; init; }
}

public record PlaylistSummary
{
    public required Guid Id { get; init; }

    /// <summary>
    /// For a system playlist (<see cref="SystemKind"/> not <c>none</c>) the frontend words the title by the kind.
    /// </summary>
    public required string Title { get; init; }

    public string? Description { get; init; }

    public required PlaylistAccess Access { get; init; }

    public required PlaylistAddPolicy AddPolicy { get; init; }

    public MusicService? SearchFirst { get; init; }

    public required PlaylistSystemKind SystemKind { get; init; }

    public required PersonRef Owner { get; init; }

    /// <summary>
    /// The caller's role; null when the caller is not a member.
    /// </summary>
    public PlaylistRole? MyRole { get; init; }

    /// <summary>
    /// Whether the caller follows it: keeps it without being a member (not while it is private).
    /// </summary>
    public bool Following { get; init; }

    /// <summary>
    /// The caller's "show in my library", as a member or a follower; null when neither.
    /// </summary>
    public bool? ShowInLibrary { get; init; }

    /// <summary>
    /// The covers of its first songs that have one, at most four (addresses the browser loads).
    /// </summary>
    public required IReadOnlyList<string> Covers { get; init; }

    public required int SongCount { get; init; }

    public required int MemberCount { get; init; }

    /// <summary>
    /// The first members (the owner first), for the faces.
    /// </summary>
    public required IReadOnlyList<PersonRef> FirstMembers { get; init; }

    /// <summary>
    /// The music service's playlist it was imported from, if it was.
    /// </summary>
    public PlaylistOrigin? Origin { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}

public record MemberContacts
{
    /// <summary>
    /// The Telegram user names of the member's sign-ins.
    /// </summary>
    public required IReadOnlyList<string> Telegram { get; init; }
}

public record PlaylistMemberItem
{
    public required PersonRef Person { get; init; }

    public required PlaylistRole Role { get; init; }

    public required DateTimeOffset JoinedAt { get; init; }

    public required MemberContacts Contacts { get; init; }
}

public record InvitationItem
{
    public required string Token { get; init; }

    public required PlaylistRole Role { get; init; }

    public PersonRef? CreatedBy { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public int? MaxUses { get; init; }

    public required int Uses { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }
}

/// <summary>
/// A playlist's page: its settings, members (with their contacts: everyone who sees the playlist sees them), what the
/// caller may do, and for its admins the invitations.
/// </summary>
/// <summary>
/// A playlist just joined. A very large one starts out of the person's library: the app tells them, and they show it
/// there themselves.
/// </summary>
public record JoinedPlaylist : PlaylistDetails
{
    public JoinedPlaylist()
    {
    }

    [SetsRequiredMembers]
    public JoinedPlaylist(PlaylistDetails details)
        : base(details)
    {
    }

    public bool KeptOutOfLibrary { get; init; }
}

public record PlaylistDetails
{
    public required PlaylistSummary Playlist { get; init; }

    public required IReadOnlyList<PlaylistMemberItem> Members { get; init; }

    public required bool CanAdd { get; init; }

    public required bool CanAdminister { get; init; }

    /// <summary>
    /// For the owner and admins; empty for others.
    /// </summary>
    public required IReadOnlyList<InvitationItem> Invitations { get; init; }

    /// <summary>
    /// The latest sync (a job of the kind <c>playlist_sync</c>; its result: <c>{ added }</c>), for the one who imported it.
    /// </summary>
    public JobInfo? Sync { get; init; }
}

public record CreatePlaylistRequest
{
    public required string Title { get; init; }

    public string? Description { get; init; }

    public PlaylistAccess Access { get; init; } = PlaylistAccess.Private;

    public PlaylistAddPolicy AddPolicy { get; init; } = PlaylistAddPolicy.AllMembers;

    public MusicService? SearchFirst { get; init; }
}

/// <summary>
/// The settings to change; what is not given stays.
/// </summary>
public record UpdatePlaylistRequest
{
    public string? Title { get; init; }

    /// <summary>
    /// An empty text clears it.
    /// </summary>
    public string? Description { get; init; }

    public PlaylistAccess? Access { get; init; }

    public PlaylistAddPolicy? AddPolicy { get; init; }

    /// <summary>
    /// Set <see cref="ClearSearchFirst"/> to clear it.
    /// </summary>
    public MusicService? SearchFirst { get; init; }

    public bool ClearSearchFirst { get; init; }
}

public record ShowInLibraryRequest
{
    public required bool Show { get; init; }
}

public record SetMemberRoleRequest
{
    /// <summary>
    /// <c>admin</c> or <c>member</c>.
    /// </summary>
    public required PlaylistRole Role { get; init; }
}

public record PassOwnershipRequest
{
    public required Guid AccountId { get; init; }
}

public record CreateInvitationRequest
{
    /// <summary>
    /// <c>member</c> or <c>admin</c>.
    /// </summary>
    public PlaylistRole Role { get; init; } = PlaylistRole.Member;

    /// <summary>
    /// 1 … 365.
    /// </summary>
    public int ExpiresInDays { get; init; } = 7;

    public int? MaxUses { get; init; }
}

/// <summary>
/// What an invitation's link shows while it may be accepted (one expired, revoked or used up answers 410
/// <c>invitation_invalid</c> and tells nothing of the playlist).
/// </summary>
public record InvitationView
{
    public required string Token { get; init; }

    public required Guid PlaylistId { get; init; }

    public required string PlaylistTitle { get; init; }

    public required PlaylistSystemKind SystemKind { get; init; }

    public required PersonRef Owner { get; init; }

    public PersonRef? InvitedBy { get; init; }

    public required PlaylistRole Role { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Whether the caller is already a member.
    /// </summary>
    public required bool AlreadyMember { get; init; }
}

/// <summary>
/// A playlist of a music service to import: by its link, or by its service and id (as <c>MusicPlaylist</c> names it —
/// one of the person's own, read with their linked account, or any other).
/// </summary>
public record ImportPlaylistRequest
{
    /// <summary>
    /// A link to a playlist of a music service. Given alone.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// The playlist's service; given with <see cref="PlaylistId"/>, instead of a link.
    /// </summary>
    public MusicService? Service { get; init; }

    /// <summary>
    /// The playlist's id on its service; given with <see cref="Service"/>, instead of a link.
    /// </summary>
    public string? PlaylistId { get; init; }
}

/// <summary>
/// An answer to a change: what it gave, and the token that undoes it for a short while (<c>POST /api/undo</c>); no
/// token when the change changed nothing.
/// </summary>
public record Changed<T>
{
    public required T Value { get; init; }

    public string? Undo { get; init; }
}

/// <summary>
/// An answer to a change that gives nothing back but its undo.
/// </summary>
public record UndoOnly
{
    public string? Undo { get; init; }
}

/// <summary>
/// What a job of the kind "playlist_sync" ends with.
/// </summary>
public record PlaylistSyncResult : JobResult
{
    /// <summary>
    /// How many songs it added.
    /// </summary>
    public required int Added { get; init; }
}
