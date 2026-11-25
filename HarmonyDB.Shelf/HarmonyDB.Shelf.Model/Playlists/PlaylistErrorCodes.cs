namespace HarmonyDB.Shelf.Model.Playlists;

/// <summary>
/// The error codes of playlists, songs, the library, marks, keys, notes, links, the sync and undo.
/// </summary>
public static class PlaylistErrorCodes
{
    /// <summary>404: no such playlist, or the caller may not see it.</summary>
    public const string NoSuchPlaylist = "no_such_playlist";

    /// <summary>404: no such song, or the caller may not see it.</summary>
    public const string NoSuchSong = "no_such_song";

    /// <summary>404: no such variant in the song.</summary>
    public const string NoSuchVariant = "no_such_variant";

    /// <summary>404: no such member.</summary>
    public const string NoSuchMember = "no_such_member";

    /// <summary>404: no such invitation (or link).</summary>
    public const string NoSuchInvitation = "no_such_invitation";

    /// <summary>404: no such link.</summary>
    public const string NoSuchLink = "no_such_link";

    /// <summary>403: for the playlist's members.</summary>
    public const string MembersOnly = "members_only";

    /// <summary>403: for the playlist's owner and admins.</summary>
    public const string AdminsOnly = "admins_only";

    /// <summary>403: for the playlist's owner.</summary>
    public const string OwnerOnly = "owner_only";

    /// <summary>403: the playlist lets only its admins add.</summary>
    public const string AddingNotAllowed = "adding_not_allowed";

    /// <summary>403: the thing is someone else's; only admins remove or change others' things.</summary>
    public const string NotYours = "not_yours";

    /// <summary>403: a system playlist cannot be deleted, left, renamed or passed on.</summary>
    public const string SystemPlaylist = "system_playlist";

    /// <summary>403: only the one who imported the playlist syncs it.</summary>
    public const string NotImportedByYou = "not_imported_by_you";

    /// <summary>409: the chords are in the playlist already (details: songId).</summary>
    public const string AlreadyInPlaylist = "already_in_playlist";

    /// <summary>409: the owner leaves only after passing the playlist on (or deletes it).</summary>
    public const string OwnerCannotLeave = "owner_cannot_leave";

    /// <summary>409: the batch is of another account than the session's.</summary>
    public const string WrongAccount = "wrong_account";

    /// <summary>409: the caller is a member already.</summary>
    public const string AlreadyMember = "already_member";

    /// <summary>409: the person imported this playlist already (details: playlistId).</summary>
    public const string AlreadyImported = "already_imported";

    /// <summary>410: the invitation expired, was revoked or used up.</summary>
    public const string InvitationInvalid = "invitation_invalid";

    /// <summary>400: the link is not a music service's playlist.</summary>
    public const string LinkNotPlaylist = "link_not_playlist";

    /// <summary>400: a link is only made of a playlist of <c>link</c> access.</summary>
    public const string NotLinkAccess = "not_link_access";

    /// <summary>400: no such kind of mark.</summary>
    public const string UnknownMarkKind = "unknown_mark_kind";

    /// <summary>400: a key is −11 … +11 semitones.</summary>
    public const string BadSemitones = "bad_semitones";

    /// <summary>400: the chords are not in the playlist.</summary>
    public const string ChordsNotInPlaylist = "chords_not_in_playlist";

    /// <summary>403: the chords are not open to the person (or not known): only chords open to a person are put into a
    /// playlist by them (details of a copied song: leftOut, how many of its variants were not open).</summary>
    public const string ChordsNotOpen = "chords_not_open";

    /// <summary>400: the text is too long.</summary>
    public const string TooLong = "too_long";

    /// <summary>A queued change made before the thing was last changed or removed: not applied (the answer carries
    /// the thing as it is now).</summary>
    public const string Stale = "stale";

    /// <summary>410: the undo is too late, or not the caller's.</summary>
    public const string UndoExpired = "undo_expired";

    /// <summary>409: there is nothing to undo any more (something took the removed thing's place).</summary>
    public const string NothingToUndo = "nothing_to_undo";
}
