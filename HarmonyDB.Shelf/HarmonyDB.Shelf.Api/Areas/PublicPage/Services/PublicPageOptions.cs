using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.PublicPage.Services;

/// <summary>
/// The configuration section <c>PublicPage</c>: the public page, for what is written for it on the server (the
/// previews of its addresses, its sitemap). Without <see cref="Origin"/> nothing is written: the page stays as built.
/// </summary>
public sealed class PublicPageOptions
{
    public const string SectionName = "PublicPage";

    /// <summary>
    /// The address the public page is opened at: scheme, host and port, no path. The previews' and the sitemap's
    /// addresses are on it.
    /// </summary>
    public string? Origin
    {
        get => _origin;
        set => _origin = string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('/');
    }

    private string? _origin;

    /// <summary>
    /// The page's own picture, on its origin: the preview's picture when the item has none.
    /// </summary>
    public string DefaultImagePath { get; set; } = "/icon-512.png";

    /// <summary>
    /// The side of <see cref="DefaultImagePath"/>, pixels (a square).
    /// </summary>
    public int DefaultImageSize { get; set; } = 512;

    /// <summary>
    /// How long a written preview is kept, for every reader of the address.
    /// </summary>
    public TimeSpan Keep { get; set; } = TimeSpan.FromMinutes(5);

    public bool IsConfigured => Origin != null;
}

internal sealed class PublicPageOptionsValidator : IValidateOptions<PublicPageOptions>
{
    public ValidateOptionsResult Validate(string? name, PublicPageOptions options)
    {
        var failures = new List<string>();
        var section = PublicPageOptions.SectionName;

        if (options.Origin != null
            && (!Uri.TryCreate(options.Origin, UriKind.Absolute, out var origin)
                || (origin.Scheme != Uri.UriSchemeHttps && origin.Scheme != Uri.UriSchemeHttp)
                || origin.AbsolutePath != "/" || origin.Query.Length > 0 || origin.Fragment.Length > 0 || origin.UserInfo.Length > 0))
            failures.Add($"{section}:{nameof(PublicPageOptions.Origin)} is not an http(s) origin with no path.");

        if (!options.DefaultImagePath.StartsWith('/') || options.DefaultImagePath.StartsWith("//", StringComparison.Ordinal))
            failures.Add($"{section}:{nameof(PublicPageOptions.DefaultImagePath)} is not a path on the page's origin.");

        if (options.DefaultImageSize <= 0)
            failures.Add($"{section}:{nameof(PublicPageOptions.DefaultImageSize)} is not positive.");

        if (options.Keep < TimeSpan.Zero)
            failures.Add($"{section}:{nameof(PublicPageOptions.Keep)} is negative.");

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
