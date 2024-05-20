using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Collectives.Api.Models;
using OneShelf.Collectives.Database.Models;

namespace OneShelf.Collectives.Api.Services;

public class UrlsManager
{
    private readonly ILogger<UrlsManager> _logger;
    private readonly CollectivesOptions _options;

    public UrlsManager(ILogger<UrlsManager> logger, IOptions<CollectivesOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public string Generate(Guid id, int searchTag, int versionNumber)
    {
        return string.Format(_options.UrlTemplate, id, searchTag, versionNumber);
    }

    public Guid GetCollectiveId(Uri collectiveUri)
    {
        return Guid.Parse(Regex.Match(collectiveUri.ToString(), _options.UrlIdPattern).Groups[1].Value);
    }

    public Uri Generate(Collective collective) =>
        new(Generate(collective.Id, collective.Versions.Last().SearchTag, collective.Versions.Count));
}