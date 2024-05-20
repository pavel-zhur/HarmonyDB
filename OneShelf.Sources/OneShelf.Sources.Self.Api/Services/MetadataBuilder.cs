using System.Text.RegularExpressions;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Sources.Api.Model.V1;
using Microsoft.Extensions.Options;
using OneShelf.Collectives.Api.Model.V2.Sub;
using OneShelf.Frontend.SpecificModel;
using OneShelf.Pdfs.SpecificModel;
using OneShelf.Sources.Self.Api.Models;

namespace OneShelf.Sources.Self.Api.Services;

public class MetadataBuilder
{
    private readonly SelfApiOptions _options;

    public MetadataBuilder(IOptions<SelfApiOptions> options)
    {
        _options = options.Value;
    }

    public string? GetExternalId(Uri uri)
    {
        if (!_options.Hosts.Contains(uri.Host))
            return null;

        try
        {
            var match = Regex.Match(uri.ToString(), "^https\\:\\/\\/[^\\/]+\\/\\?id\\=([0-9a-f\\-]{36})\\&tag\\=([0-9]{6})\\&version\\=[0-9]+$");

            return $"mj-{match.Groups[2].Value}-{match.Groups[1].Value}";
        }
        catch
        {
            return null;
        }
    }

    public bool IsExternalId(string externalId) => Regex.IsMatch(externalId, "^mj\\-[0-9]{6}-[0-9a-f\\-]{36}$");

    public Guid GetCollectiveId(string externalId) =>
        Guid.Parse(Regex.Match(externalId, "^mj\\-[0-9]{6}-([0-9a-f\\-]{36})$").Groups[1].Value);

    public Chords GetChords(CollectiveVersion version, NodeHtml nodeHtml)
    {
        var specificAttributes = new Dictionary<string, dynamic>();
        new PdfsAttributes
        {
            ShortSourceName = _options.ShortSourceName,
        }.ToDictionary(specificAttributes);

        new FrontendAttributesV1
        {
            BadgeText = null,
            SourceColor = SourceColor.Success,
        }.ToDictionary(specificAttributes);

        return new()
        {
            Title = version.Collective.Title,
            Artists = version.Collective.Authors,
            BestTonality = version.Collective.Tonality,
            Source = _options.SourceName,
            ExternalId = GetExternalId(version.Uri) ?? throw new($"Could not parse the uri {version.Uri}."),
            IsStable = true,
            Output = nodeHtml,
            Rating = null,
            SourceUri = version.Uri,
            Type = null,
            SpecificAttributes = specificAttributes,
            IsPublic = true,
            UnstableErrorMessage = null,
        };
    }
}