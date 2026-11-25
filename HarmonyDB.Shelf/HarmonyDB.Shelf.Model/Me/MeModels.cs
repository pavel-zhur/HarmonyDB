namespace HarmonyDB.Shelf.Model.Me;

public record Profile
{
    public required Guid Id { get; init; }

    /// <summary>
    /// From the provider of the latest sign-in.
    /// </summary>
    public required string DisplayName { get; init; }

    public string? PictureUrl { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The built-in permissions in force.
    /// </summary>
    public required IReadOnlyList<string> Permissions { get; init; }
}

public enum Accidentals
{
    /// <summary>As each chord is written.</summary>
    AsWritten,
    Flats,
    Sharps,
}

public enum Theme
{
    System,
    Light,
    Dark,
}

/// <summary>
/// The person's settings of the app; written whole.
/// </summary>
public record Settings
{
    /// <summary>
    /// The language of the interface (one of the public configuration's locales); null: the browser's.
    /// </summary>
    public string? Locale { get; init; }

    /// <summary>
    /// The size of the chords, 0.25 … 4.
    /// </summary>
    public required double ChordsScale { get; init; }

    /// <summary>
    /// The size of the whole interface, 0.25 … 4.
    /// </summary>
    public required double InterfaceScale { get; init; }

    /// <summary>
    /// "Less": a tap on a chord shows a small plate instead of the window of fingerings.
    /// </summary>
    public required bool LessMode { get; init; }

    public required Accidentals Accidentals { get; init; }

    public required bool ShowDegrees { get; init; }

    /// <summary>
    /// Alterations of chord names as superscripts and subscripts.
    /// </summary>
    public required bool ChordSuperscripts { get; init; }

    public required Theme Theme { get; init; }
}

public record SessionItem
{
    public required Guid Id { get; init; }

    /// <summary>
    /// The session of this very request.
    /// </summary>
    public required bool Current { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset SignedInAt { get; init; }

    public required DateTimeOffset LastSeenAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public string? UserAgent { get; init; }

    public string? IpAddress { get; init; }
}

/// <summary>
/// A sign-in linked to the account.
/// </summary>
public record LoginItem
{
    public required Guid Id { get; init; }

    /// <summary>
    /// "telegram", "google", "yandex", "dev".
    /// </summary>
    public required string Provider { get; init; }

    public string? DisplayName { get; init; }

    public string? Username { get; init; }

    public string? Email { get; init; }

    public string? PictureUrl { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset LastSignInAt { get; init; }
}

public enum MusicService
{
    Yandex,
    Youtube,
}

/// <summary>
/// A linked music account. What was copied from the browser is never given back.
/// </summary>
public record MusicLinkItem
{
    public required MusicService Service { get; init; }

    /// <summary>
    /// The account's name on the service.
    /// </summary>
    public string? ExternalName { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastUsedAt { get; init; }
}

/// <summary>
/// Links a music account (replacing the one linked before for the service).
/// </summary>
public record PutMusicLinkRequest
{
    /// <summary>
    /// What the person copied from the browser: yandex — the token; youtube — the request headers of a signed-in
    /// YouTube Music page.
    /// </summary>
    public required string Credentials { get; init; }
}
