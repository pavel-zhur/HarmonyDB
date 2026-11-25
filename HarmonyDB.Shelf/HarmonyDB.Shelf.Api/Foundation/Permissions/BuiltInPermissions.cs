namespace HarmonyDB.Shelf.Api.Foundation.Permissions;

/// <summary>
/// The permissions the code knows. Custom ones come from configuration (<see cref="PermissionsOptions.Custom"/>).
/// </summary>
public static class BuiltInPermissions
{
    /// <summary>MCP: music of Yandex, anonymously or by the service account.</summary>
    public const string McpYandexAnonymous = "mcp.yandex.anonymous";

    /// <summary>MCP: music of Yandex, with the person's own linked account.</summary>
    public const string McpYandexLinked = "mcp.yandex.linked";

    /// <summary>MCP: music of YouTube, anonymously or by the service account.</summary>
    public const string McpYoutubeAnonymous = "mcp.youtube.anonymous";

    /// <summary>MCP: music of YouTube, with the person's own linked account.</summary>
    public const string McpYoutubeLinked = "mcp.youtube.linked";

    /// <summary>MCP: the index — search chords, open them, resolve links, the analysis tools.</summary>
    public const string McpIndex = "mcp.index";

    /// <summary>MCP: read the library (playlists, songs, variants, marks, keys, notes, recent).</summary>
    public const string McpLibraryRead = "mcp.library.read";

    /// <summary>MCP: change the library, deleting included.</summary>
    public const string McpLibraryWrite = "mcp.library.write";

    /// <summary>MCP: PDF books.</summary>
    public const string McpPdf = "mcp.pdf";

    /// <summary>PDF books in the app.</summary>
    public const string Pdf = "pdf";

    /// <summary>Generating illustrations.</summary>
    public const string IllustrationsGenerate = "illustrations.generate";

    public static IReadOnlyList<string> All { get; } =
    [
        McpYoutubeAnonymous,
        McpYoutubeLinked,
        McpYandexAnonymous,
        McpYandexLinked,
        McpIndex,
        McpLibraryRead,
        McpLibraryWrite,
        McpPdf,
        Pdf,
        IllustrationsGenerate,
    ];
}
