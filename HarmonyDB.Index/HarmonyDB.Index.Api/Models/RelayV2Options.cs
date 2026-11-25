using Microsoft.Extensions.Options;

namespace HarmonyDB.Index.Api.Models;

/// <summary>
/// The sources the index relays the source contract, version 2, to (configuration section <see cref="SectionName"/>).
/// </summary>
public class RelayV2Options
{
    public const string SectionName = "RelayV2";

    public List<RelayV2Source> Sources { get; set; } = [];

    /// <summary>
    /// How long one source may take to answer one operation; a source that takes longer is reported as not answering.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// One source: where it is, the index's internal key there, and the prefixes of the ids it gives out.
/// </summary>
public class RelayV2Source
{
    /// <summary>
    /// The name of the source in this configuration; it names the source in the search failures.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The base address of its operations, ending with a slash.
    /// </summary>
    public Uri? Endpoint { get; set; }

    public string Key { get; set; } = string.Empty;

    public List<string> IdPrefixes { get; set; } = [];
}

internal class RelayV2OptionsValidator : IValidateOptions<RelayV2Options>
{
    public ValidateOptionsResult Validate(string? name, RelayV2Options options)
    {
        var failures = new List<string>();

        // without sources every search would look like "nothing found"
        if (options.Sources.Count == 0)
            failures.Add($"{RelayV2Options.SectionName}:{nameof(RelayV2Options.Sources)}: there is no source.");

        if (options.Timeout <= TimeSpan.Zero)
            failures.Add($"{RelayV2Options.SectionName}:{nameof(RelayV2Options.Timeout)} is not positive.");

        for (var i = 0; i < options.Sources.Count; i++)
        {
            var source = options.Sources[i];
            var where = $"{RelayV2Options.SectionName}:{nameof(RelayV2Options.Sources)}:{i}";

            if (string.IsNullOrWhiteSpace(source.Name))
                failures.Add($"{where}: the source has no name.");

            if (source.Endpoint is not { IsAbsoluteUri: true } || !source.Endpoint.AbsolutePath.EndsWith('/'))
                failures.Add($"{where} ({source.Name}): the endpoint is not an absolute address ending with a slash.");

            if (string.IsNullOrEmpty(source.Key))
                failures.Add($"{where} ({source.Name}): the key is empty.");

            if (source.IdPrefixes.Count == 0 || source.IdPrefixes.Any(string.IsNullOrEmpty))
                failures.Add($"{where} ({source.Name}): the id prefixes are missing or empty.");
        }

        if (options.Sources.GroupBy(x => x.Name, StringComparer.Ordinal).Any(g => g.Count() > 1))
            failures.Add($"{RelayV2Options.SectionName}: two sources have the same name.");

        // an id must route to one source only
        var prefixes = options.Sources
            .SelectMany(s => s.IdPrefixes.Where(p => !string.IsNullOrEmpty(p)).Select(p => (source: s.Name, prefix: p)))
            .ToList();
        for (var i = 0; i < prefixes.Count; i++)
        for (var j = 0; j < prefixes.Count; j++)
        {
            if (i != j && prefixes[j].prefix.StartsWith(prefixes[i].prefix, StringComparison.Ordinal))
                failures.Add($"{RelayV2Options.SectionName}: the id prefix '{prefixes[j].prefix}' ({prefixes[j].source}) begins with '{prefixes[i].prefix}' ({prefixes[i].source}).");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
