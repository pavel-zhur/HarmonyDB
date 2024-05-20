using OneShelf.Common.Api.Client;

namespace HarmonyDB.Sources.Api.Client;

public class SourcesApiClientOptions
{
    internal IReadOnlyList<SourceOptions> Sources { get; init; } = null!;

    internal class SourceOptions : ApiClientOptions<SourceApiClient>
    {
        public required List<string> Sources { get; init; }

        public required bool SupportsSearch { get; init; }
    }
}