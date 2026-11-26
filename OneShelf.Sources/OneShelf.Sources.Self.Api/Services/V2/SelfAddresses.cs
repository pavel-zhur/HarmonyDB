using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OneShelf.Sources.Self.Api.Models;

namespace OneShelf.Sources.Self.Api.Services.V2;

/// <summary>
/// The addresses of own chords, of three parts: the collective, the six-digit code and the version's number. Two shapes
/// are read: the store's (the old shelf's page, on <see cref="SelfApiOptions.Hosts"/>) and this source's own
/// (<see cref="SelfV2Options.AddressTemplate"/>), which is the one given. Addresses are compared by their parts, never as
/// whole strings: a link to the old page and one to the new are the same key, and what else a link carries (query
/// parameters a messenger or a site added, a fragment) is let be.
/// </summary>
public sealed class SelfAddresses(IOptions<SelfApiOptions> selfOptions, IOptions<SelfV2Options> options)
{
    public sealed record Parts(Guid CollectiveId, int Code, int Version);

    private const string StoreTemplate = "https://store.invalid/?id={id}&tag={code}&version={version}";
    private const string StoreIdPattern = "[0-9a-f\\-]{36}";
    private const string OwnIdPattern = "[a-z]+-[0-9a-f\\-]{36}";

    /// <summary>
    /// Whether a template has each of its three parts once and makes an absolute address.
    /// </summary>
    public static bool IsTemplate(string template)
        => new[] { SelfV2Options.IdPlaceholder, SelfV2Options.CodePlaceholder, SelfV2Options.VersionPlaceholder }.All(x => template.Split(x).Length == 2)
           && Uri.TryCreate(Fill(template, SelfV2Source.ToId(Guid.Empty), 123456, 1), UriKind.Absolute, out _);

    /// <summary>
    /// The address this source gives of the version.
    /// </summary>
    public Uri Make(Guid collectiveId, int code, int version) => new(Fill(options.Value.AddressTemplate!, SelfV2Source.ToId(collectiveId), code, version));

    /// <summary>
    /// The parts of an address of either shape; null when it is not an address of own chords.
    /// </summary>
    public Parts? Read(Uri uri)
    {
        if (!uri.IsAbsoluteUri)
            return null;

        if (uri.Scheme == Uri.UriSchemeHttps && selfOptions.Value.Hosts.Contains(uri.Host)
            && Shape.Of(StoreTemplate, StoreIdPattern).Match(uri, sameAuthority: false) is { } store)
            return Parse(store.Id, store.Code, store.Version);

        if (Shape.Of(options.Value.AddressTemplate!, OwnIdPattern).Match(uri, sameAuthority: true) is { } own && SelfV2Source.FromId(own.Id) is { } collectiveId)
            return Parse(collectiveId.ToString("D"), own.Code, own.Version);

        return null;
    }

    private static Parts? Parse(string collectiveId, string code, string version)
        => Guid.TryParseExact(collectiveId, "D", out var id) && int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var c)
                                                           && int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out var v)
            ? new(id, c, v)
            : null;

    private static string Fill(string template, string id, int code, int version)
        => template
            .Replace(SelfV2Options.IdPlaceholder, Uri.EscapeDataString(id), StringComparison.Ordinal)
            .Replace(SelfV2Options.CodePlaceholder, code.ToString("000000", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace(SelfV2Options.VersionPlaceholder, version.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>
    /// A template read as an address: its scheme, host and port; its path; and its query parameters, each by its name.
    /// An address matches when those are the same (the three parts where the template has them) — its other query
    /// parameters and its fragment are not looked at.
    /// </summary>
    private sealed class Shape
    {
        private const string IdMark = "IDPART";
        private const int CodeMark = 111111;
        private const int VersionMark = 222222;

        private readonly string _authority;
        private readonly Regex _path;
        private readonly IReadOnlyList<(string Name, Regex Value)> _query;

        private Shape(string authority, Regex path, IReadOnlyList<(string Name, Regex Value)> query)
        {
            _authority = authority;
            _path = path;
            _query = query;
        }

        public static Shape Of(string template, string idPattern)
        {
            // the template made into an address with marks in place of its parts, the marks then made groups
            var uri = new Uri(Fill(template, IdMark, CodeMark, VersionMark));
            Regex Pattern(string part) => new("^" + Regex.Escape(part)
                .Replace(IdMark, $"(?<id>{idPattern})")
                .Replace(CodeMark.ToString(CultureInfo.InvariantCulture), "(?<code>[0-9]{6})")
                .Replace(VersionMark.ToString(CultureInfo.InvariantCulture), "(?<version>[0-9]+)") + "$", RegexOptions.CultureInvariant);

            return new(
                uri.GetLeftPart(UriPartial.Authority),
                Pattern(uri.AbsolutePath),
                Query(uri).Select(x => (x.Name, Pattern(x.Value))).ToList());
        }

        public (string Id, string Code, string Version)? Match(Uri uri, bool sameAuthority)
        {
            if (sameAuthority && !string.Equals(uri.GetLeftPart(UriPartial.Authority), _authority, StringComparison.OrdinalIgnoreCase))
                return null;

            var groups = new Dictionary<string, string>();
            if (!Take(_path.Match(uri.AbsolutePath)))
                return null;

            var query = Query(uri).ToLookup(x => x.Name, x => x.Value);
            foreach (var (name, value) in _query)
            {
                if (query[name].ToList() is not [var given] || !Take(value.Match(given)))
                    return null;
            }

            return groups.TryGetValue("id", out var id) && groups.TryGetValue("code", out var code) && groups.TryGetValue("version", out var version)
                ? (id, code, version)
                : null;

            bool Take(Match match)
            {
                if (!match.Success)
                    return false;
                foreach (var group in new[] { "id", "code", "version" })
                {
                    if (match.Groups[group].Success)
                        groups[group] = match.Groups[group].Value;
                }

                return true;
            }
        }

        private static IEnumerable<(string Name, string Value)> Query(Uri uri)
            => uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Split('=', 2))
                .Select(x => (Uri.UnescapeDataString(x[0]), Uri.UnescapeDataString(x.Length == 2 ? x[1] : string.Empty)));
    }
}