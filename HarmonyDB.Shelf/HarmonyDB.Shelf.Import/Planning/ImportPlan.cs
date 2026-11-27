using HarmonyDB.Shelf.Database.Model;

namespace HarmonyDB.Shelf.Import.Planning;

/// <summary>
/// What the new shelf is to have of the old one, in the new model's terms, with the people as their Telegram user ids
/// and the chords as the sources' ids. Every thing has a stable key made of the old data's ids, by which a later run
/// knows it.
/// </summary>
public sealed record ImportPlan
{
    public required IReadOnlyList<PersonPlan> People { get; init; }

    public required IReadOnlyList<PlaylistPlan> Playlists { get; init; }

    public required IReadOnlyList<MarkPlan> Marks { get; init; }

    public required IReadOnlyList<KeyPlan> Keys { get; init; }

    public required IReadOnlyList<NotePlan> Notes { get; init; }

    public required IReadOnlyList<RecentPlan> Recent { get; init; }

    public required IReadOnlyList<PreferencePlan> Preferences { get; init; }

    /// <summary>
    /// The old address each chords were resolved by, by the source's id, for every chords but own chords (an own chords'
    /// address may open them to whoever holds it): kept in the chords' row, so that chords their source does not give
    /// are shown with their page.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Addresses { get; init; }

    /// <summary>
    /// What could not be mapped, with the reason.
    /// </summary>
    public required IReadOnlyList<Unmapped> Unmapped { get; init; }

    /// <summary>
    /// What is taken as it is but should be looked at.
    /// </summary>
    public required IReadOnlyList<Unmapped> Remarks { get; init; }
}

public sealed record PersonPlan(long TelegramId, string DisplayName, DateTimeOffset? CreatedAt, IReadOnlyList<string> Permissions);

public sealed record PlaylistPlan
{
    public required string Key { get; init; }

    /// <summary>
    /// The title; null for a system playlist (it has the kind's own).
    /// </summary>
    public required string? Title { get; init; }

    public required PlaylistAccess Access { get; init; }

    public required PlaylistAddPolicy AddPolicy { get; init; }

    public required PlaylistSystemKind SystemKind { get; init; }

    public required long OwnerTelegramId { get; init; }

    /// <summary>
    /// The members besides the owner.
    /// </summary>
    public required IReadOnlyList<MemberPlan> Members { get; init; }

    public required IReadOnlyList<SongPlan> Songs { get; init; }
}

public sealed record MemberPlan(long TelegramId, PlaylistRole Role);

/// <summary>
/// A song of a playlist; <see cref="Key"/> is unique in the playlist.
/// </summary>
public sealed record SongPlan(string Key, string Title, IReadOnlyList<string> Artists, double Position, long? AddedBy, DateTimeOffset? AddedAt, IReadOnlyList<ItemPlan> Items);

/// <summary>
/// A chords variant of a song, in the order of the variants (letters A, B, C …).
/// </summary>
/// <param name="Address">The old version's address: what the entry reads its chords by.</param>
/// <param name="Private">An author's private own chords: the entry is seen by whoever added it alone.</param>
public sealed record ItemPlan(string SourceId, long? AddedBy, DateTimeOffset? AddedAt, string? Address = null, bool Private = false);

/// <param name="At">When the person liked it in the old shelf, when known.</param>
public sealed record MarkPlan(long TelegramId, string SourceId, string Kind, DateTimeOffset? At);

/// <summary>
/// A transposition: "mine" (no playlist) or "here" in a playlist.
/// </summary>
/// <param name="At">When the key was set in the old shelf, when known.</param>
public sealed record KeyPlan(long TelegramId, string SourceId, string? PlaylistKey, int Semitones, DateTimeOffset? At);

/// <summary>
/// A note: on a song of a playlist (<see cref="PlaylistKey"/> and <see cref="SongKey"/>), or personal, on chords
/// (<see cref="SourceId"/>); made of the person's comments there, in their order.
/// </summary>
public sealed record NotePlan(long TelegramId, string? PlaylistKey, string? SongKey, string? SourceId, IReadOnlyList<NotePart> Parts)
{
    /// <summary>
    /// When the last of its comments was written, when known.
    /// </summary>
    public DateTimeOffset? At => Parts.Select(x => x.At).Max();
}

/// <summary>
/// A comment in a note; <see cref="SourceId"/>: the chords it was written on (on a song of several variants the part
/// begins with the variant's letter as it stands), null for a comment on the song.
/// </summary>
public sealed record NotePart(string Text, string? SourceId, DateTimeOffset? At);

/// <param name="Title">What the person was shown when they opened it (the old shelf kept it with the visit).</param>
public sealed record RecentPlan(long TelegramId, string SourceId, DateTimeOffset OpenedAt, string? Title = null, IReadOnlyList<string>? Artists = null, string? Source = null);

/// <summary>
/// The variant a song of several opened at for a person in the old shelf: their main variant, where it is not the song's
/// first as the song stands.
/// </summary>
public sealed record PreferencePlan(long TelegramId, string PlaylistKey, string SongKey, string SourceId);

/// <summary>
/// Something of the old shelf that is not in the plan. <see cref="Pending"/>: it waits for its address to be resolved
/// (a later run takes it); otherwise it is not taken at all.
/// </summary>
public sealed record Unmapped(string Kind, long? TelegramId, string What, string Reason, bool Pending = false);

public static class UnmappedReasons
{
    public const string AddressNotResolved = "address not resolved";
    public const string SongNotShown = "the song was archived or postponed";
    public const string SongWithoutVersions = "the song has no chords";
    public const string NoVersionNoLevel = "a like of a song with no version and no level";
    public const string TranspositionWithoutVersion = "a transposition of a song with no version (never applied)";
    public const string NotAPerson = "not a person (a service or test user)";
    public const string TenantNotConfigured = "a tenant of several people that is not configured";
    public const string TenantNotImported = "configured as not imported (its people get an account when they sign in)";
    public const string OrphanTenantWithoutPerson = "a tenant with no user whose person is unknown or not taken";
    public const string DuplicateInPlaylist = "the same chords are already in another song of the playlist";

    public const string OwnChordsWithoutAddress = "own chords no version refers to";
    public const string OwnChordsDeleted = "own chords deleted";
    public const string ListOwnerNotTaken = "the list's person is not taken";
    public const string Misdecoded = "the title or an artist looks mis-decoded in the old shelf; taken as it is";
    public const string UnknownLevel = "a like of a level the shelf has no heart for";
    public const string HereDiffersFromMine = "the key in the list differs from the person's key elsewhere (the list's is \"here\" there)";
    public const string MergedHeart = "another like of the person lands on the same chords (the highest level is kept)";
}
