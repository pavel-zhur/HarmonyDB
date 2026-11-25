using System.Text.Encodings.Web;
using HarmonyDB.Shelf.Api.Areas.Auth.Services;
using HarmonyDB.Shelf.Api.Areas.OAuth.Services;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Mcp.Services;

/// <summary>
/// Who calls the MCP server: <c>Authorization: Bearer …</c> with an access token the shelf gave for this server (its
/// audience, the resource, is this server's address), or with a personal key (<c>shk_…</c>). The identity is the
/// person's, delegated (<see cref="McpPrincipals"/>): what is in force is what the person allowed the client (or the
/// key) and what the admin gives the person now, both read on every request — a permission taken away by the admin
/// stops working at once, a disconnected client or a revoked key at once too. Used on <c>/mcp</c> only; the browser's
/// session never acts there.
/// </summary>
public sealed class McpBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ShelfDbContext db,
    McpPrincipals principals,
    McpAddresses addresses,
    TimeProvider time)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    public const string SchemeName = McpPrincipals.SchemeName;

    public const string PersonalKeyPrefix = "shk_";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = header["Bearer ".Length..].Trim();
        if (token.Length == 0)
            return AuthenticateResult.Fail("invalid_token");

        var hash = AuthTokens.Hash(token);
        var now = time.GetUtcNow();
        var resource = addresses.Resource;
        var cancellationToken = Context.RequestAborted;

        var found = token.StartsWith(PersonalKeyPrefix, StringComparison.Ordinal)
            ? await principals.ByKeyAsync(db.PersonalKeys.Where(x => x.KeyHash == hash && (x.ExpiresAt == null || x.ExpiresAt > now)).Select(x => x.Id), cancellationToken)
            : await principals.ByGrantAsync(
                db.OAuthTokens.Where(x => x.TokenHash == hash && x.Kind == OAuthTokenKind.Access && x.ExpiresAt > now && x.Resource == resource).Select(x => x.GrantId),
                cancellationToken);
        if (found == null)
            return AuthenticateResult.Fail("invalid_token");

        await principals.TouchAsync(found, cancellationToken);
        return AuthenticateResult.Success(new(found.Principal, SchemeName));
    }
}
