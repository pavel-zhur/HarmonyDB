using HarmonyDB.Analyses.Api.Services;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Analyses.Api.Models;

/// <summary>
/// The module's settings (section <see cref="SectionName"/> of the module's configuration): how the source labels its
/// items, its public address, and the folder of its files. All but the picture are required.
/// </summary>
public sealed class AnalysesOptions
{
    public const string SectionName = nameof(AnalysesOptions);

    /// <summary>
    /// The scope of the internal clients that read the analyses as a source (the index).
    /// </summary>
    public const string SourceScope = "source";

    /// <summary>
    /// The scope of the internal clients that put analyses in and take them away.
    /// </summary>
    public const string WriteScope = "analyses.write";

    public const string IdPlaceholder = "{id}";

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
    /// begins with the analyses' id prefix: a picture is routed to its source as an id is.
    /// </summary>
    public string? IconName { get; set; }

    /// <summary>
    /// The picture's file: a PNG, an SVG or a WebP.
    /// </summary>
    public string? IconFile { get; set; }

    /// <summary>
    /// An analysis's public page, with <see cref="IdPlaceholder"/> where its id goes (<c>https://…/chords/{id}</c>): its
    /// item's address.
    /// </summary>
    public string? PublicUriTemplate { get; set; }

    /// <summary>
    /// The folder the analyses' files are kept in, one folder an analysis.
    /// </summary>
    public string? FilesFolder { get; set; }

    /// <summary>
    /// The largest file taken in, bytes.
    /// </summary>
    public long MaxFileBytes { get; set; } = 300L * 1024 * 1024;

    /// <summary>
    /// An analysis's public page, by <see cref="PublicUriTemplate"/>.
    /// </summary>
    public Uri PublicUri(string id) => new(PublicUriTemplate!.Replace(IdPlaceholder, Uri.EscapeDataString(id), StringComparison.Ordinal));

    /// <summary>
    /// The id whose public page this address is; null when it is not one.
    /// </summary>
    public string? IdOfPublicUri(Uri uri)
    {
        var parts = PublicUriTemplate!.Split(IdPlaceholder);
        var text = uri.AbsoluteUri;
        if (!text.StartsWith(parts[0], StringComparison.Ordinal) || !text.EndsWith(parts[1], StringComparison.Ordinal) || text.Length < parts[0].Length + parts[1].Length)
            return null;

        var id = Uri.UnescapeDataString(text[parts[0].Length..^parts[1].Length]);
        return id.StartsWith(AnalysisStore.IdPrefix, StringComparison.Ordinal) && PublicUri(id).AbsoluteUri == text ? id : null;
    }

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

        if (string.IsNullOrWhiteSpace(options.SourceKey))
            failures.Add($"{section}:{nameof(AnalysesOptions.SourceKey)} is not set.");

        if (string.IsNullOrWhiteSpace(options.SourceTitle))
            failures.Add($"{section}:{nameof(AnalysesOptions.SourceTitle)} is not set.");

        if (string.IsNullOrWhiteSpace(options.IconName) != string.IsNullOrWhiteSpace(options.IconFile))
            failures.Add($"{section}:{nameof(AnalysesOptions.IconName)} and {nameof(AnalysesOptions.IconFile)} are set together.");

        if (!string.IsNullOrWhiteSpace(options.IconName) && !options.IconName.StartsWith(AnalysisStore.IdPrefix, StringComparison.Ordinal))
            failures.Add($"{section}:{nameof(AnalysesOptions.IconName)} does not begin with the analyses' id prefix '{AnalysisStore.IdPrefix}'.");

        if (!string.IsNullOrWhiteSpace(options.IconFile))
        {
            if (options.IconContentType == null)
                failures.Add($"{section}:{nameof(AnalysesOptions.IconFile)} is not a PNG, an SVG or a WebP file.");
            else if (!File.Exists(options.IconFile))
                failures.Add($"{section}:{nameof(AnalysesOptions.IconFile)} does not exist: {options.IconFile}.");
        }

        if (string.IsNullOrWhiteSpace(options.PublicUriTemplate)
            || options.PublicUriTemplate.Split(AnalysesOptions.IdPlaceholder).Length != 2
            || !Uri.TryCreate(options.PublicUriTemplate.Replace(AnalysesOptions.IdPlaceholder, AnalysisStore.IdPrefix + "0", StringComparison.Ordinal), UriKind.Absolute, out var sample)
            || (sample.Scheme != Uri.UriSchemeHttps && sample.Scheme != Uri.UriSchemeHttp))
            failures.Add($"{section}:{nameof(AnalysesOptions.PublicUriTemplate)} is not an http(s) address with {AnalysesOptions.IdPlaceholder} once in it.");

        if (string.IsNullOrWhiteSpace(options.FilesFolder) || !Directory.Exists(options.FilesFolder))
            failures.Add($"{section}:{nameof(AnalysesOptions.FilesFolder)} is not an existing folder.");

        if (options.MaxFileBytes <= 0)
            failures.Add($"{section}:{nameof(AnalysesOptions.MaxFileBytes)} is not positive.");

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
