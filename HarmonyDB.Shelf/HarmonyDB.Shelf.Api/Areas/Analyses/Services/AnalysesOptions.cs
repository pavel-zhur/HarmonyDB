using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Analyses.Services;

/// <summary>
/// The configuration section <c>Analyses</c>: how the shelf's own analyses are labelled as a source. Without a title the
/// source is not configured, and its endpoints answer 503 <c>unavailable</c>.
/// </summary>
public sealed class AnalysesOptions
{
    public const string SectionName = "Analyses";

    /// <summary>
    /// The source's stable key, as its items' label gives it.
    /// </summary>
    public string? SourceKey { get; set; }

    /// <summary>
    /// The source's name to show.
    /// </summary>
    public string? SourceTitle { get; set; }

    /// <summary>
    /// The opaque name of the source's picture, when it has one; the picture is the file <see cref="IconFile"/>. It
    /// begins with the analyses' id prefix (<see cref="AnalysisStore.IdPrefix"/>): a picture is routed to its source as
    /// an id is.
    /// </summary>
    public string? IconName { get; set; }

    /// <summary>
    /// The picture's file: a PNG, an SVG or a WebP.
    /// </summary>
    public string? IconFile { get; set; }

    /// <summary>
    /// The address of an analysis on the public page, with <c>{id}</c> where its id goes
    /// (<c>https://…/chords/{id}</c>): its items' "link to the source". Not set: their headers give no address.
    /// </summary>
    public string? PublicUriTemplate
    {
        get => _publicUriTemplate;
        set => _publicUriTemplate = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private string? _publicUriTemplate;

    /// <summary>
    /// The largest file of an analysis taken in, bytes.
    /// </summary>
    public long MaxFileBytes { get; set; } = 300L * 1024 * 1024;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SourceTitle);

    public const string IdPlaceholder = "{id}";

    /// <summary>
    /// The analysis's address on the public page, by <see cref="PublicUriTemplate"/>; null when it is not set.
    /// </summary>
    public Uri? PublicUri(string id)
        => PublicUriTemplate == null ? null : new Uri(PublicUriTemplate.Replace(IdPlaceholder, Uri.EscapeDataString(id), StringComparison.Ordinal));

    /// <summary>
    /// The picture's content type, by its file's extension.
    /// </summary>
    public string? IconContentType => Path.GetExtension(IconFile)?.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".svg" => "image/svg+xml",
        ".webp" => "image/webp",
        _ => null,
    };
}

internal sealed class AnalysesOptionsValidator : IValidateOptions<AnalysesOptions>
{
    public ValidateOptionsResult Validate(string? name, AnalysesOptions options)
    {
        var failures = new List<string>();
        var section = AnalysesOptions.SectionName;

        if (options.IsConfigured && string.IsNullOrWhiteSpace(options.SourceKey))
            failures.Add($"{section}:{nameof(AnalysesOptions.SourceKey)} is not set.");

        if (!options.IsConfigured && (options.SourceKey != null || options.IconName != null || options.IconFile != null || options.PublicUriTemplate != null))
            failures.Add($"{section}:{nameof(AnalysesOptions.SourceTitle)} is not set.");

        if (string.IsNullOrWhiteSpace(options.IconName) != string.IsNullOrWhiteSpace(options.IconFile))
            failures.Add($"{section}:{nameof(AnalysesOptions.IconName)} and {nameof(AnalysesOptions.IconFile)} are set together.");

        // a picture's name routes to its source as an id does: by the source's prefix
        if (!string.IsNullOrWhiteSpace(options.IconName) && !options.IconName.StartsWith(AnalysisStore.IdPrefix, StringComparison.Ordinal))
            failures.Add($"{section}:{nameof(AnalysesOptions.IconName)} does not begin with the analyses' id prefix '{AnalysisStore.IdPrefix}'.");

        if (!string.IsNullOrWhiteSpace(options.IconFile))
        {
            if (options.IconContentType == null)
                failures.Add($"{section}:{nameof(AnalysesOptions.IconFile)} is not a PNG, an SVG or a WebP file.");
            else if (!File.Exists(options.IconFile))
                failures.Add($"{section}:{nameof(AnalysesOptions.IconFile)} does not exist: {options.IconFile}.");
        }

        if (options.PublicUriTemplate != null
            && (options.PublicUriTemplate.Split(AnalysesOptions.IdPlaceholder).Length != 2
                || !Uri.TryCreate(options.PublicUriTemplate.Replace(AnalysesOptions.IdPlaceholder, AnalysisStore.IdPrefix + "0", StringComparison.Ordinal), UriKind.Absolute, out var sample)
                || (sample.Scheme != Uri.UriSchemeHttps && sample.Scheme != Uri.UriSchemeHttp)))
            failures.Add($"{section}:{nameof(AnalysesOptions.PublicUriTemplate)} is not an http(s) address with {AnalysesOptions.IdPlaceholder} once in it.");

        if (options.MaxFileBytes <= 0)
            failures.Add($"{section}:{nameof(AnalysesOptions.MaxFileBytes)} is not positive.");

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
