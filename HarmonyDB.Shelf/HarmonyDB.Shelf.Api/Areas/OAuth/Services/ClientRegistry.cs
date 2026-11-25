using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyDB.Shelf.Api.Areas.Auth.Services;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Mcp;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Areas.OAuth.Services;

/// <summary>
/// The clients of the authorization server. Two ways in, as the MCP authorization specification (2026-07-28) has them:
/// <list type="bullet">
/// <item>a client id metadata document — the client's id is the https address of its own description, read when it
/// asks for an authorization (the way the specification prefers);</item>
/// <item>dynamic client registration (RFC 7591) — kept by the specification for the clients that do not have the
/// first.</item>
/// </list>
/// Either way a client is a row of <c>oauth_clients</c>; a described one is written anew from its description at each
/// authorization. Where a client may be sent back: https addresses, or http ones on this computer
/// (<c>localhost</c>, <c>127.0.0.1</c>, <c>[::1]</c>), whose port may differ at each request (OAuth 2.1, native apps).
/// </summary>
public sealed class ClientRegistry(ShelfDbContext db, ClientMetadataFetcher fetcher, TimeProvider time)
{
    public const string AuthNone = "none";
    public const string AuthSecretBasic = "client_secret_basic";
    public const string AuthSecretPost = "client_secret_post";

    public const string GrantAuthorizationCode = "authorization_code";
    public const string GrantRefreshToken = "refresh_token";

    private const int MaxRedirectUris = 20;

    /// <summary>
    /// The longest name of a client, in characters.
    /// </summary>
    public const int MaxName = 100;

    /// <summary>
    /// Whether a client's name (trimmed) may be shown to a person as it is: not too long, and no character that is not
    /// seen as itself — control characters, line breaks, and invisible or direction-changing ones (which could make the
    /// name read as something else). Such a name is refused, not cleaned: the client is told so, and a person is never
    /// shown a name other than the one it gave.
    /// </summary>
    public static bool IsShowableName(string name)
        => name.Length <= MaxName
           && name.All(x => char.GetUnicodeCategory(x) is not (UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator));

    /// <summary>
    /// Whether a client id is the address of a description document, in the form the draft requires: https, a path, no
    /// user name or password, no fragment, no dot segments — checked on the raw text, which must be the address exactly
    /// as it reads normalized (so nothing in it is folded away).
    /// </summary>
    public static bool IsMetadataDocumentId(string clientId)
        => Uri.TryCreate(clientId, UriKind.Absolute, out var uri)
           && uri.Scheme == Uri.UriSchemeHttps
           && uri.AbsolutePath.Length > 1
           && uri.UserInfo.Length == 0
           && uri.Fragment.Length == 0
           && !clientId.Contains('#') && !clientId.Contains('@')
           && uri.AbsoluteUri == clientId;

    public static McpClientKind KindOf(string clientId) => IsMetadataDocumentId(clientId) ? McpClientKind.MetadataDocument : McpClientKind.Registered;

    /// <summary>
    /// The name a client gave; null when it gave none (RFC 7591 does not require one).
    /// </summary>
    public static string? NameOf(OAuthClient client) => client.ClientName;

    /// <summary>
    /// The host of a described client's address, in its ASCII form (a look-alike letter of another script shows as such).
    /// </summary>
    public static string? IdHostOf(OAuthClient client) => IsMetadataDocumentId(client.ClientId) ? new Uri(client.ClientId).IdnHost : null;

    /// <summary>
    /// How long a client that nobody connected is kept.
    /// </summary>
    public static readonly TimeSpan UnusedClientLifetime = TimeSpan.FromDays(1);

    /// <summary>
    /// Deletes the clients made over a day ago that no person connected (anyone may register; nothing else ends them).
    /// </summary>
    private Task PurgeUnusedAsync(CancellationToken cancellationToken)
    {
        var before = time.GetUtcNow() - UnusedClientLifetime;
        return db.OAuthClients.Where(x => x.CreatedAt < before && !db.OAuthGrants.Any(g => g.ClientId == x.ClientId)).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Registers a client (RFC 7591).
    /// </summary>
    public async Task<(OAuthClient Client, string? Secret)> RegisterAsync(RegistrationRequest request, CancellationToken cancellationToken)
    {
        var redirectUris = request.RedirectUris ?? [];
        if (redirectUris.Count == 0 || redirectUris.Count > MaxRedirectUris || !redirectUris.All(IsAllowedRedirect))
            throw new OAuthError("invalid_redirect_uri", "redirect_uris: one or more https addresses, or http ones on localhost.");
        if (!string.IsNullOrWhiteSpace(request.ClientName) && !IsShowableName(request.ClientName.Trim()))
            throw new OAuthError("invalid_client_metadata", $"client_name: at most {MaxName} characters, with no control, invisible or direction-changing characters.");

        var grantTypes = request.GrantTypes is { Count: > 0 } given ? given.Distinct().ToList() : [GrantAuthorizationCode];
        if (!grantTypes.Contains(GrantAuthorizationCode) || grantTypes.Any(x => x is not (GrantAuthorizationCode or GrantRefreshToken)))
            throw new OAuthError("invalid_client_metadata", "grant_types: authorization_code, and refresh_token if wanted.");
        if (request.ResponseTypes is { Count: > 0 } responseTypes && responseTypes.Any(x => x != "code"))
            throw new OAuthError("invalid_client_metadata", "response_types: code only.");

        var authMethod = request.TokenEndpointAuthMethod ?? AuthSecretBasic;
        if (authMethod is not (AuthNone or AuthSecretBasic or AuthSecretPost))
            throw new OAuthError("invalid_client_metadata", "token_endpoint_auth_method: none, client_secret_basic or client_secret_post.");

        var secret = authMethod == AuthNone ? null : AuthTokens.NewToken();
        var client = new OAuthClient
        {
            ClientId = "c_" + AuthTokens.NewToken()[..24],
            ClientName = string.IsNullOrWhiteSpace(request.ClientName) ? null : request.ClientName.Trim(),
            RedirectUris = redirectUris.ToList(),
            GrantTypes = grantTypes,
            TokenEndpointAuthMethod = authMethod,
            ClientSecretHash = secret == null ? null : AuthTokens.Hash(secret),
            ClientUri = HttpsOrNull(request.ClientUri),
            LogoUri = HttpsOrNull(request.LogoUri),
            SoftwareId = Short(request.SoftwareId),
            SoftwareVersion = Short(request.SoftwareVersion),
            CreatedAt = time.GetUtcNow(),
        };
        await PurgeUnusedAsync(cancellationToken);
        db.OAuthClients.Add(client);
        await db.SaveChangesAsync(cancellationToken);
        return (client, secret);
    }

    /// <summary>
    /// The client asking for an authorization: a registered one, or a described one read from its address now and
    /// kept. Null when it is not known or its description is not valid.
    /// </summary>
    public async Task<OAuthClient?> ResolveForAuthorizationAsync(string clientId, CancellationToken cancellationToken)
    {
        if (!IsMetadataDocumentId(clientId))
            return await db.OAuthClients.SingleOrDefaultAsync(x => x.ClientId == clientId, cancellationToken);

        var document = await fetcher.FetchAsync(new Uri(clientId), cancellationToken);
        if (document is not { } json || Read(json, clientId) is not { } described)
            return null;

        await PurgeUnusedAsync(cancellationToken);
        var client = await db.OAuthClients.SingleOrDefaultAsync(x => x.ClientId == clientId, cancellationToken);
        if (client == null)
        {
            client = new() { ClientId = clientId, ClientName = described.ClientName, TokenEndpointAuthMethod = AuthNone, CreatedAt = time.GetUtcNow() };
            db.OAuthClients.Add(client);
        }

        client.ClientName = described.ClientName;
        client.RedirectUris = described.RedirectUris;
        client.GrantTypes = described.GrantTypes;
        client.TokenEndpointAuthMethod = AuthNone;
        client.ClientSecretHash = null;
        client.ClientUri = described.ClientUri;
        client.LogoUri = described.LogoUri;
        client.SoftwareId = described.SoftwareId;
        client.SoftwareVersion = described.SoftwareVersion;
        await db.SaveChangesAsync(cancellationToken);
        return client;
    }

    /// <summary>
    /// A client's description, valid by the draft: its <c>client_id</c> is exactly its address, it has a name and
    /// addresses to go back to, and it authenticates with nothing (a public client: no shared secrets in a document
    /// anyone can read; signed assertions are not taken here).
    /// </summary>
    private static Described? Read(JsonElement json, string address)
    {
        string? Text(string name) => json.TryGetProperty(name, out var x) && x.ValueKind == JsonValueKind.String ? x.GetString() : null;
        // a list of texts; an item that is not a text makes it no list
        List<string>? Texts(string name) => json.TryGetProperty(name, out var x) && x.ValueKind == JsonValueKind.Array
                                            && x.EnumerateArray().All(y => y.ValueKind == JsonValueKind.String)
            ? x.EnumerateArray().Select(y => y.GetString()!).ToList()
            : null;

        if (Text("client_id") != address)
            return null;

        var name = Text("client_name");
        var redirectUris = Texts("redirect_uris");
        if (string.IsNullOrWhiteSpace(name) || !IsShowableName(name.Trim())
            || redirectUris is not { Count: > 0 and <= MaxRedirectUris } || !redirectUris.All(IsAllowedRedirect))
            return null;

        if (Text("token_endpoint_auth_method") is { } method && method != AuthNone)
            return null;

        var grantTypes = Texts("grant_types") is { Count: > 0 } given ? given : [GrantAuthorizationCode];
        if (!grantTypes.Contains(GrantAuthorizationCode) || grantTypes.Any(x => x is not (GrantAuthorizationCode or GrantRefreshToken)))
            return null;

        return new(name.Trim(), redirectUris, grantTypes, HttpsOrNull(Text("client_uri")), HttpsOrNull(Text("logo_uri")), Short(Text("software_id")), Short(Text("software_version")));
    }

    /// <summary>
    /// An address a client may be sent back to: absolute, no fragment; https, or http on this computer.
    /// </summary>
    public static bool IsAllowedRedirect(string redirectUri)
        => Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)
           && string.IsNullOrEmpty(uri.Fragment)
           && redirectUri.Length <= 2000
           && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && IsLoopback(uri.Host)));

    /// <summary>
    /// Whether the address asked is one the client registered: exactly, or — for http on this computer — but for the
    /// port.
    /// </summary>
    public static bool Matches(OAuthClient client, string redirectUri)
        => Uri.TryCreate(redirectUri, UriKind.Absolute, out var given)
           && client.RedirectUris.Any(registered => registered == redirectUri
                                                    || (Uri.TryCreate(registered, UriKind.Absolute, out var known)
                                                        && known.Scheme == Uri.UriSchemeHttp && given.Scheme == Uri.UriSchemeHttp
                                                        && IsLoopback(known.Host) && known.Host == given.Host
                                                        && known.PathAndQuery == given.PathAndQuery
                                                        && string.IsNullOrEmpty(given.Fragment)));

    public static bool IsLoopback(string host)
        => host == "localhost" || (IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));

    private static string? HttpsOrNull(string? uri)
        => uri != null && uri.Length <= 2000 && Uri.TryCreate(uri, UriKind.Absolute, out var x) && x.Scheme == Uri.UriSchemeHttps ? uri : null;

    private static string? Short(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Length <= 200 ? text : text[..200];

    private sealed record Described(
        string ClientName, List<string> RedirectUris, List<string> GrantTypes, string? ClientUri, string? LogoUri, string? SoftwareId, string? SoftwareVersion);
}

/// <summary>
/// A client's registration (RFC 7591, the fields the shelf reads).
/// </summary>
public sealed record RegistrationRequest
{
    [JsonPropertyName("redirect_uris")]
    public List<string>? RedirectUris { get; init; }

    [JsonPropertyName("client_name")]
    public string? ClientName { get; init; }

    [JsonPropertyName("grant_types")]
    public List<string>? GrantTypes { get; init; }

    [JsonPropertyName("response_types")]
    public List<string>? ResponseTypes { get; init; }

    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; init; }

    [JsonPropertyName("client_uri")]
    public string? ClientUri { get; init; }

    [JsonPropertyName("logo_uri")]
    public string? LogoUri { get; init; }

    [JsonPropertyName("software_id")]
    public string? SoftwareId { get; init; }

    [JsonPropertyName("software_version")]
    public string? SoftwareVersion { get; init; }

    /// <summary>
    /// Read and not kept: the shelf is not an OpenID provider, its rules of redirect addresses are the same for all.
    /// </summary>
    [JsonPropertyName("application_type")]
    public string? ApplicationType { get; init; }
}

/// <summary>
/// An OAuth error answer (<c>{ error, error_description }</c>), from the token, registration and revocation endpoints.
/// </summary>
public sealed class OAuthError(string error, string description, int statusCode = StatusCodes.Status400BadRequest) : Exception(error)
{
    public string Error { get; } = error;

    public string Description { get; } = description;

    public int StatusCode { get; } = statusCode;
}
