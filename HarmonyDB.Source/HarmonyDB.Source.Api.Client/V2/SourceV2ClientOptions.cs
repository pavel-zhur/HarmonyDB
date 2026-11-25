namespace HarmonyDB.Source.Api.Client.V2;

/// <summary>
/// Where a server of the source contract, version 2, is (a source, or the index that relays to sources) and how to call
/// it.
/// </summary>
public class SourceV2ClientOptions
{
    /// <summary>
    /// The name the caller knows the server by; it names the server in <see cref="Model.V2.Api.SearchFailure.Source"/>.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The base address of the operations, ending with a slash: <c>http://host/module/api/</c>.
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// The caller's internal key at that server, sent in the <c>X-Internal-Key</c> header.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// How long an operation may take before the server is taken as not answering.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
