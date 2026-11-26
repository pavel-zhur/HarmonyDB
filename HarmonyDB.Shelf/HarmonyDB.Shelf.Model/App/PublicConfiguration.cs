using HarmonyDB.Shelf.Model.Marks;

namespace HarmonyDB.Shelf.Model.App;

/// <summary>
/// What the frontend needs to know at start, before anyone signs in.
/// </summary>
public record PublicConfiguration
{
    /// <summary>
    /// The sign-in providers that are on, in the order to offer them.
    /// </summary>
    public required IReadOnlyList<SignInProviderInfo> SignInProviders { get; init; }

    /// <summary>
    /// The languages of the interface ("en", "ru").
    /// </summary>
    public required IReadOnlyList<string> Locales { get; init; }

    /// <summary>
    /// The language when neither the person nor the browser names one of <see cref="Locales"/>.
    /// </summary>
    public required string DefaultLocale { get; init; }

    /// <summary>
    /// The kinds of marks, in the order they are offered: key (what is stored and sent), sign (what is drawn), whether
    /// it is one of the hearts.
    /// </summary>
    public required IReadOnlyList<MarkKind> MarkKinds { get; init; }

    /// <summary>
    /// The playlist the public page shows as its showcase (<c>GET /api/playlists/{id}</c>); null when there is none.
    /// </summary>
    public Guid? ShowcasePlaylistId { get; init; }

    /// <summary>
    /// The public page's origin (<c>https://…</c>, no slash at the end).
    /// </summary>
    public required string PublicPageOrigin { get; init; }
}

public record SignInProviderInfo
{
    /// <summary>
    /// "telegram", "google", "yandex", "dev".
    /// </summary>
    public required string Name { get; init; }
}
