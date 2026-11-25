using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HarmonyDB.Shelf.Api.Areas.Auth.Services;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.OAuth.Services;

/// <summary>
/// The token endpoint (authorization code with PKCE; refresh token) and revocation (RFC 7009).
/// <list type="bullet">
/// <item>Tokens are random, kept as their hashes only. An access token is short-lived and bound to the MCP server as
/// its audience (the resource, RFC 8707); a refresh token is used once and replaced by a new one of the same family.</item>
/// <item>A code is used once: a code presented a second time revokes every token it gave (its family). A refresh token
/// presented after it was replaced is taken as stolen: its whole family is revoked.</item>
/// <item>What a token may do is not in it: it names its grant, and the grant's permissions are intersected with the
/// person's on every call.</item>
/// </list>
/// </summary>
public sealed partial class TokenService(
    ShelfDbContext db,
    AuthorizationRequests requests,
    McpAddresses addresses,
    IOptionsMonitor<OAuthOptions> options,
    TimeProvider time)
{
    public async Task<TokenResponse> ExchangeAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var client = await AuthenticateClientAsync(request, form, cancellationToken);
        if (One(form, "resource") is { } resource && !requests.SameResource(resource))
            throw new OAuthError("invalid_target", $"This server issues tokens for {addresses.Resource} only.");

        return One(form, "grant_type") switch
        {
            ClientRegistry.GrantAuthorizationCode => await RedeemCodeAsync(client, form, cancellationToken),
            ClientRegistry.GrantRefreshToken when client.GrantTypes.Contains(ClientRegistry.GrantRefreshToken) => await RefreshAsync(client, form, cancellationToken),
            ClientRegistry.GrantRefreshToken => throw new OAuthError("unauthorized_client", "The client did not register the refresh_token grant."),
            _ => throw new OAuthError("unsupported_grant_type", "grant_type: authorization_code or refresh_token."),
        };
    }

    private async Task<TokenResponse> RedeemCodeAsync(OAuthClient client, IFormCollection form, CancellationToken cancellationToken)
    {
        var hash = AuthTokens.Hash(One(form, "code") ?? throw new OAuthError("invalid_request", "code is required."));
        var code = await db.OAuthAuthorizationCodes.AsNoTracking()
            .Where(x => x.CodeHash == hash && x.Grant.ClientId == client.ClientId)
            .Select(x => new { x.GrantId, x.FamilyId, x.RedirectUri, x.CodeChallenge, x.ExpiresAt, Deleted = x.Grant.Account.DeletedAt != null })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw InvalidGrant();

        var now = time.GetUtcNow();
        var consumed = await db.OAuthAuthorizationCodes
            .Where(x => x.CodeHash == hash && x.ConsumedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(c => c.ConsumedAt, now), cancellationToken);
        if (consumed == 0)
        {
            // used before: whatever it gave is revoked
            await db.OAuthTokens.Where(x => x.FamilyId == code.FamilyId).ExecuteDeleteAsync(cancellationToken);
            throw InvalidGrant();
        }

        var verifier = One(form, "code_verifier");
        if (code.ExpiresAt <= now || code.Deleted
            || One(form, "redirect_uri") != code.RedirectUri
            || verifier == null || !PkceVerifierFormat().IsMatch(verifier)
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(AuthTokens.PkceChallenge(verifier)), Encoding.ASCII.GetBytes(code.CodeChallenge)))
            throw InvalidGrant();

        return await IssueAsync(client, code.GrantId, code.FamilyId, now, cancellationToken);
    }

    private async Task<TokenResponse> RefreshAsync(OAuthClient client, IFormCollection form, CancellationToken cancellationToken)
    {
        var hash = AuthTokens.Hash(One(form, "refresh_token") ?? throw new OAuthError("invalid_request", "refresh_token is required."));
        var token = await db.OAuthTokens.AsNoTracking()
            .Where(x => x.TokenHash == hash && x.Kind == OAuthTokenKind.Refresh && x.Grant.ClientId == client.ClientId)
            .Select(x => new { x.GrantId, x.FamilyId, x.ExpiresAt, Deleted = x.Grant.Account.DeletedAt != null })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw InvalidGrant();

        var now = time.GetUtcNow();
        var consumed = await db.OAuthTokens
            .Where(x => x.TokenHash == hash && x.ConsumedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.ConsumedAt, now), cancellationToken);
        if (consumed == 0)
        {
            // replaced before and presented again: taken as stolen, the family is revoked
            await db.OAuthTokens.Where(x => x.FamilyId == token.FamilyId).ExecuteDeleteAsync(cancellationToken);
            throw InvalidGrant();
        }

        if (token.ExpiresAt <= now || token.Deleted)
            throw InvalidGrant();

        return await IssueAsync(client, token.GrantId, token.FamilyId, now, cancellationToken);
    }

    private async Task<TokenResponse> IssueAsync(OAuthClient client, Guid grantId, Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        var grant = await db.OAuthGrants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == grantId, cancellationToken) ?? throw InvalidGrant();

        // the grant's spent and expired codes and tokens go
        await db.OAuthTokens.Where(x => x.GrantId == grantId && x.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
        await db.OAuthAuthorizationCodes.Where(x => x.GrantId == grantId && x.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);

        var access = AuthTokens.NewToken();
        db.OAuthTokens.Add(new()
        {
            TokenHash = AuthTokens.Hash(access),
            GrantId = grantId,
            Kind = OAuthTokenKind.Access,
            FamilyId = familyId,
            Resource = addresses.Resource,
            CreatedAt = now,
            ExpiresAt = now + settings.AccessTokenLifetime,
        });

        string? refresh = null;
        if (client.GrantTypes.Contains(ClientRegistry.GrantRefreshToken))
        {
            refresh = AuthTokens.NewToken();
            db.OAuthTokens.Add(new()
            {
                TokenHash = AuthTokens.Hash(refresh),
                GrantId = grantId,
                Kind = OAuthTokenKind.Refresh,
                FamilyId = familyId,
                Resource = addresses.Resource,
                CreatedAt = now,
                ExpiresAt = now + settings.RefreshTokenLifetime,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await db.OAuthClients.Where(x => x.ClientId == client.ClientId).ExecuteUpdateAsync(x => x.SetProperty(c => c.LastUsedAt, now), cancellationToken);

        return new()
        {
            AccessToken = access,
            ExpiresIn = (int)settings.AccessTokenLifetime.TotalSeconds,
            RefreshToken = refresh,
            Scope = string.Join(' ', McpPermissions.Of(grant.Permissions)),
        };
    }

    /// <summary>
    /// Revokes a token of the client (RFC 7009): a refresh token with its family, an access token alone. A token that
    /// is not known, or not the client's, is answered the same.
    /// </summary>
    public async Task RevokeAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var client = await AuthenticateClientAsync(request, form, cancellationToken);
        if (One(form, "token") is not { } value)
            throw new OAuthError("invalid_request", "token is required.");

        var hash = AuthTokens.Hash(value);
        var token = await db.OAuthTokens.AsNoTracking()
            .Where(x => x.TokenHash == hash && x.Grant.ClientId == client.ClientId)
            .Select(x => new { x.Kind, x.FamilyId })
            .SingleOrDefaultAsync(cancellationToken);
        if (token == null)
            return;

        if (token.Kind == OAuthTokenKind.Refresh)
            await db.OAuthTokens.Where(x => x.FamilyId == token.FamilyId).ExecuteDeleteAsync(cancellationToken);
        else
            await db.OAuthTokens.Where(x => x.TokenHash == hash).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// The client at the token or revocation endpoint: a public one names itself (<c>client_id</c>); a confidential one
    /// proves itself by the way it registered (Basic, or the secret in the form).
    /// </summary>
    private async Task<OAuthClient> AuthenticateClientAsync(HttpRequest request, IFormCollection form, CancellationToken cancellationToken)
    {
        string? clientId = One(form, "client_id"), secret = One(form, "client_secret"), method = secret == null ? ClientRegistry.AuthNone : ClientRegistry.AuthSecretPost;
        if (AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out var header) && header.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase))
        {
            string decoded;
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter ?? string.Empty));
            }
            catch (FormatException)
            {
                throw InvalidClient();
            }

            // each part form-encoded (RFC 6749, 2.3.1)
            static string FormDecode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));
            var colon = decoded.IndexOf(':');
            if (colon < 0 || secret != null || (clientId != null && clientId != FormDecode(decoded[..colon])))
                throw InvalidClient();

            clientId = FormDecode(decoded[..colon]);
            secret = FormDecode(decoded[(colon + 1)..]);
            method = ClientRegistry.AuthSecretBasic;
        }

        var client = clientId == null ? null : await db.OAuthClients.AsNoTracking().SingleOrDefaultAsync(x => x.ClientId == clientId, cancellationToken);
        if (client == null || client.TokenEndpointAuthMethod != method)
            throw InvalidClient();

        if (method != ClientRegistry.AuthNone
            && (client.ClientSecretHash == null || !CryptographicOperations.FixedTimeEquals(AuthTokens.Hash(secret!), client.ClientSecretHash)))
            throw InvalidClient();

        return client;
    }

    private static string? One(IFormCollection form, string name)
        => form[name].Count switch
        {
            0 => null,
            1 => form[name][0],
            _ => throw new OAuthError("invalid_request", $"{name} is given more than once."),
        };

    private static OAuthError InvalidGrant() => new("invalid_grant", "The code or refresh token is not valid, expired, already used, or was not given to this client.");

    private static OAuthError InvalidClient() => new("invalid_client", "The client is not known or did not prove itself as it registered.", StatusCodes.Status401Unauthorized);

    /// <summary>
    /// A PKCE verifier (RFC 7636): 43 … 128 characters of the unreserved set.
    /// </summary>
    [GeneratedRegex("^[A-Za-z0-9._~-]{43,128}$")]
    private static partial Regex PkceVerifierFormat();
}

/// <summary>
/// The token endpoint's answer (OAuth 2.1).
/// </summary>
public sealed record TokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public required int ExpiresIn { get; init; }

    [JsonPropertyName("refresh_token")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("scope")]
    public required string Scope { get; init; }
}
