using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Api.Foundation.SignIn;
using HarmonyDB.Shelf.Model;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.OAuth.Services;

/// <summary>
/// The public addresses of the MCP server and of its authorization server, all at the app's origin
/// (<see cref="SignInOptions.PublicOrigin"/>): the issuer is the origin itself, the protected resource is
/// <c>{origin}/mcp</c>. The origin is what the person opens; the app's proxy passes these paths on to the shelf.
/// </summary>
public sealed class McpAddresses(IOptionsMonitor<SignInOptions> signIn)
{
    /// <summary>
    /// The path of the MCP server.
    /// </summary>
    public const string McpPath = "/mcp";

    /// <summary>
    /// The path of its protected resource metadata (RFC 9728: the well-known prefix put before the resource's path).
    /// </summary>
    public const string ResourceMetadataPath = "/.well-known/oauth-protected-resource" + McpPath;

    public const string AuthorizationServerMetadataPath = "/.well-known/oauth-authorization-server";

    public const string AuthorizePath = "/oauth/authorize";
    public const string TokenPath = "/oauth/token";
    public const string RegisterPath = "/oauth/register";
    public const string RevokePath = "/oauth/revoke";

    /// <summary>
    /// Where the files given to assistants are fetched by their short-lived links.
    /// </summary>
    public const string FilesPath = McpPath + "/files";

    /// <summary>
    /// The page of the app where the person decides on an assistant's request (the frontend's).
    /// </summary>
    public const string ConsentPagePath = "/connect";

    /// <exception cref="ShelfException">503 <c>unavailable</c>: the origin is not configured.</exception>
    public string Origin => signIn.CurrentValue.PublicOrigin
                            ?? throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);

    public string Issuer => Origin;

    /// <summary>
    /// The MCP server's canonical address: the resource its tokens are bound to (RFC 8707).
    /// </summary>
    public string Resource => Origin + McpPath;

    public string ResourceMetadata => Origin + ResourceMetadataPath;

    public string Url(string path) => Origin + path;

    public string ConsentPage(string requestId) => $"{Origin}{ConsentPagePath}?request={Uri.EscapeDataString(requestId)}";

    public string ConsentError(string code) => $"{Origin}{ConsentPagePath}?error={Uri.EscapeDataString(code)}";
}

/// <summary>
/// The permissions an assistant or a key may be given: the MCP ones. They are also the OAuth scopes of the MCP server.
/// A person's custom keys are not given to a client by choice: a client always reads the sources with the keys the
/// person has (see <see cref="AllowedToClient"/>).
/// </summary>
public static class McpPermissions
{
    public static IReadOnlyList<string> All { get; } =
    [
        BuiltInPermissions.McpIndex,
        BuiltInPermissions.McpLibraryRead,
        BuiltInPermissions.McpLibraryWrite,
        BuiltInPermissions.McpPdf,
        BuiltInPermissions.McpYandexAnonymous,
        BuiltInPermissions.McpYandexLinked,
        BuiltInPermissions.McpYoutubeAnonymous,
        BuiltInPermissions.McpYoutubeLinked,
    ];

    public static bool IsMcp(string permission) => All.Contains(permission, StringComparer.Ordinal);

    /// <summary>
    /// What a client acting for the person is allowed, to intersect with what the admin gives the person: what the
    /// person allowed it, and every custom key (so that it reads the sources as the person does).
    /// </summary>
    public static IReadOnlyCollection<string> AllowedToClient(IEnumerable<string> allowedByPerson, PermissionCatalog catalog)
        => allowedByPerson.Concat(catalog.All.Where(x => !x.IsBuiltIn).Select(x => x.Key)).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The MCP permissions among these, in the order of <see cref="All"/>.
    /// </summary>
    public static IReadOnlyList<string> Of(IEnumerable<string> permissions)
    {
        var set = permissions.ToHashSet(StringComparer.Ordinal);
        return All.Where(set.Contains).ToList();
    }
}
