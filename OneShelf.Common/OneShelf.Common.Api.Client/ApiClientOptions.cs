namespace OneShelf.Common.Api.Client;

public class ApiClientOptions<TClient> : ApiClientOptionsEndpoint<TClient>
{
    public Dictionary<string, ApiClientOptionsEndpoint<TClient>>? ConditionalStreams { get; set; }

    public Uri GetEndpoint(string? conditionalStreamId)
        => (conditionalStreamId == null
               ? Endpoint
               : ConditionalStreams?.GetValueOrDefault(conditionalStreamId)?.Endpoint ?? Endpoint)
           ?? throw new($"The endpoint for conditional stream '{conditionalStreamId}' is not configured, the default fallback value is not available, either.");

    public string? GetMasterCode(string? conditionalStreamId)
        => conditionalStreamId == null
            ? MasterCode
            : ConditionalStreams?.GetValueOrDefault(conditionalStreamId)?.MasterCode ?? MasterCode;

    public string GetServiceCode(string? conditionalStreamId)
        => (conditionalStreamId == null
               ? ServiceCode
               : ConditionalStreams?.GetValueOrDefault(conditionalStreamId)?.ServiceCode ?? ServiceCode)
           ?? throw new($"The service code for conditional stream '{conditionalStreamId}' is not configured, the default fallback value is not available, either.");
}