using HarmonyDB.Source.Api.Client;
using OneShelf.Common.Api.Client;

namespace HarmonyDB.Index.DownstreamApi.Client;

public class DownstreamApiClientOptions
{
    public IReadOnlyList<SourceOptions> Sources { get; init; } = null!;

    public class SourceOptions : ApiClientOptions<SourceApiClient>
    {
        public required List<string> Sources { get; init; }

        public required bool SupportsSearch { get; init; }
    }
}