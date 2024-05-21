using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Index.BusinessLogic.Services;

public class SourceResolver
{
    private readonly IndexApiOptions _options;

    public SourceResolver(IOptions<IndexApiOptions> options)
    {
        _options = options.Value;
    }

    public string GetSource(string externalId) => _options.SourcesExternalIdPrefixes.Single(p => externalId.StartsWith(p.Value)).Key;
}