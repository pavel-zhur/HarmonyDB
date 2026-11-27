namespace HarmonyDB.Shelf.Import.Old;

/// <summary>
/// What is read of the old shelf's database: one consistent snapshot, in the old model's terms.
/// </summary>
public sealed record OldShelf
{
    public required IReadOnlyList<OldTenant> Tenants { get; init; }

    public required IReadOnlyList<OldUser> Users { get; init; }

    public required IReadOnlyList<OldSong> Songs { get; init; }

    public required IReadOnlyList<OldVersion> Versions { get; init; }

    public required IReadOnlyList<OldLike> Likes { get; init; }

    public required IReadOnlyList<OldList> Lists { get; init; }

    public required IReadOnlyList<OldComment> Comments { get; init; }

    /// <summary>
    /// The chords a person opened: one row per person and address (or id), the last time.
    /// </summary>
    public required IReadOnlyList<OldVisit> Visits { get; init; }

    public required IReadOnlyList<OldOwnChords> OwnChords { get; init; }
}

public sealed record OldTenant(int Id, string? PrivateDescription, IReadOnlyList<string> Tags, bool ArePdfsAllowed, int LatestUsedIndex);

/// <summary>
/// A user of the old shelf; its id is the person's Telegram user id (ids of zero and below are not people).
/// </summary>
public sealed record OldUser(long Id, int TenantId, string Title, bool IllustrationsAllowed, DateTime CreatedOn);

public static class OldSongStatus
{
    public const string Draft = "Draft";
    public const string Postponed = "Postponed";
    public const string Live = "Live";
    public const string Archived = "Archived";
}

public sealed record OldSong(int Id, int TenantId, int Index, string Title, IReadOnlyList<string> Artists, string Status, long CreatedByUserId, DateTime CreatedOn);

public static class OldCollectiveType
{
    public const string Private = "Private";
    public const string Public = "Public";
}

/// <summary>
/// A version of a song: chords by their address. <see cref="CollectiveType"/> is set for own chords.
/// </summary>
public sealed record OldVersion(int Id, int SongId, string Uri, long UserId, DateTime CreatedOn, string? PublishedSettings, string? CollectiveType, Guid? CollectiveId, bool IsDefaultTemplate);

/// <summary>
/// A like: on a song (no version) or a version; with a level 1–3, or 0 (a version kept with a key, or a row of a list).
/// </summary>
public sealed record OldLike(int Id, long UserId, int SongId, int? VersionId, int Level, int? Transpose, DateTime CreatedOn, int? ListId);

public static class OldListAccess
{
    public const string Private = "Private";
    public const string SharedView = "SharedView";
    public const string SharedEditMulti = "SharedEditMulti";
    public const string SharedEditSingle = "SharedEditSingle";
}

/// <summary>
/// A list (a like category) of a person, in a tenant.
/// </summary>
public sealed record OldList(int Id, int TenantId, long UserId, string Name, int Order, string Access);

public sealed record OldComment(int Id, long UserId, int SongId, int? VersionId, string Text, DateTime CreatedOn);

/// <summary>
/// Chords a person opened: by address, or, when the visit kept no address, by the id it was opened by.
/// </summary>
/// <param name="Title">The latest row's title, artists and source: what the person was shown.</param>
public sealed record OldVisit(long UserId, string? Uri, string? ExternalId, DateTime LastViewedOn, string Title = "", IReadOnlyList<string>? Artists = null, string? Source = null);

/// <summary>
/// Own chords: written by a person (<see cref="AuthorUserId"/>), with the title and authors of their latest version.
/// </summary>
public sealed record OldOwnChords(Guid Id, long AuthorUserId, bool IsDeleted, string Title, IReadOnlyList<string> Authors, DateTime CreatedOn);
