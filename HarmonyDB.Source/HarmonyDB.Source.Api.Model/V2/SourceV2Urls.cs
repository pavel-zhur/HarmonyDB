namespace HarmonyDB.Source.Api.Model.V2;

/// <summary>
/// The operations of the source contract, version 2. Each is a POST of its request type, answered with its response
/// type, except <see cref="V2GetFile"/> (a <c>GET</c> or <c>HEAD</c>, answered with the file) and <see cref="V2GetIcon"/>
/// (answered with the picture itself).
/// </summary>
public static class SourceV2Urls
{
    /// <summary><see cref="Api.SearchRequest"/> → <see cref="Api.SearchResponse"/>.</summary>
    public const string V2Search = nameof(V2Search);

    /// <summary><see cref="Api.GetHeadersRequest"/> → <see cref="Api.GetHeadersResponse"/>.</summary>
    public const string V2GetHeaders = nameof(V2GetHeaders);

    /// <summary><see cref="Api.GetContentRequest"/> → <see cref="Api.GetContentResponse"/>.</summary>
    public const string V2GetContent = nameof(V2GetContent);

    /// <summary><see cref="Api.GetContentsRequest"/> → <see cref="Api.GetContentsResponse"/>.</summary>
    public const string V2GetContents = nameof(V2GetContents);

    /// <summary><see cref="Api.ResolveRequest"/> → <see cref="Api.ResolveResponse"/>.</summary>
    public const string V2Resolve = nameof(V2Resolve);

    /// <summary><see cref="Api.GetSourcesRequest"/> → <see cref="Api.GetSourcesResponse"/>.</summary>
    public const string V2GetSources = nameof(V2GetSources);

    /// <summary><c>GET</c> or <c>HEAD</c> with <see cref="Api.GetFileRequest"/> in its header → the file (see there).</summary>
    public const string V2GetFile = nameof(V2GetFile);

    /// <summary><see cref="Api.GetIconRequest"/> → the picture, with its content type.</summary>
    public const string V2GetIcon = nameof(V2GetIcon);

    /// <summary>No body → no body; answers when the source works.</summary>
    public const string V2Ping = nameof(V2Ping);
}
