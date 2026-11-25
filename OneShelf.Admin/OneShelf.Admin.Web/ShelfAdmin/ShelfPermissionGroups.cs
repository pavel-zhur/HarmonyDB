namespace OneShelf.Admin.Web.ShelfAdmin;

/// <summary>
/// How the shelf's built-in permissions are laid out on the pages: grouped, with a plain meaning and a short name for the
/// list. A built-in permission not named here (a new one) is shown under "Other" by its key.
/// </summary>
public static class ShelfPermissionGroups
{
    public sealed record Permission(string Key, string Short, string Meaning);

    /// <summary>
    /// A music service through the MCP: without the person's own account, and with it.
    /// </summary>
    public sealed record MusicService(string Name, string LinkService, Permission Anonymous, Permission Linked);

    public static IReadOnlyList<MusicService> McpMusic { get; } =
    [
        new("YouTube", "youtube",
            new("mcp.youtube.anonymous", "YouTube without account", "YouTube without the person's own account (anonymously or by the service account)"),
            new("mcp.youtube.linked", "YouTube linked", "YouTube with the person's own linked account: their playlists, likes, history")),
        new("Yandex", "yandex",
            new("mcp.yandex.anonymous", "Yandex without account", "Yandex without the person's own account (anonymously or by the service account)"),
            new("mcp.yandex.linked", "Yandex linked", "Yandex with the person's own linked account: their playlists, likes, history")),
    ];

    public static IReadOnlyList<Permission> McpOther { get; } =
    [
        new("mcp.index", "index", "The index: search chords, open them, resolve links, the analysis tools"),
        new("mcp.library.read", "library read", "Read the library: playlists, songs, variants, marks, keys, notes, recent"),
        new("mcp.library.write", "library write", "Change the library, deleting included"),
        new("mcp.pdf", "PDF", "PDF books"),
    ];

    public static IReadOnlyList<Permission> App { get; } =
    [
        new("pdf", "PDF", "PDF books"),
        new("illustrations.generate", "illustrations", "Generate illustrations"),
    ];

    public static IEnumerable<Permission> Mcp => McpMusic.SelectMany(x => new[] { x.Anonymous, x.Linked }).Concat(McpOther);

    private static readonly IReadOnlySet<string> Named = Mcp.Concat(App).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);

    public static bool IsNamed(string key) => Named.Contains(key);
}
