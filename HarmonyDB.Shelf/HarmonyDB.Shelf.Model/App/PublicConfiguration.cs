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
}

public record SignInProviderInfo
{
    /// <summary>
    /// "telegram", "google", "yandex", "dev".
    /// </summary>
    public required string Name { get; init; }
}
