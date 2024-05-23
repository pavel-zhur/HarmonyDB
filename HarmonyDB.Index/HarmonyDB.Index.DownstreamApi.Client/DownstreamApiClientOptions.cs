using HarmonyDB.Source.Api.Client;
using OneShelf.Common.Api.Client;

namespace HarmonyDB.Index.DownstreamApi.Client;

public class DownstreamApiClientOptions
{
    public IReadOnlyList<DownstreamSourceOptions> DownstreamSources { get; init; } = null!;

    public class DownstreamSourceOptions : ApiClientOptions<SourceApiClient>
    {
        public required List<SourceOptions> Sources { get; init; }

        public required bool IsSearchSupported { get; init; }

        public required bool IsSearchDelegated { get; init; }

        public required bool AreProgressionsProvidedForIndexing { get; init; }
    }

    public class SourceOptions
    {
        public required string Key { get; set; }

        public required string ExternalIdPrefix { get; set; }

        public required string Title { get; set; }
    }
}