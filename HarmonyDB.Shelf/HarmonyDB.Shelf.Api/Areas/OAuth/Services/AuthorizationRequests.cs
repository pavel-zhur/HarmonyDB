using System.Security.Claims;
using System.Text.RegularExpressions;
using HarmonyDB.Shelf.Api.Areas.Auth.Services;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.OAuth.Services;

/// <summary>
/// The authorization endpoint and the consent (authorization code with PKCE, OAuth 2.1).
/// <para>
/// The browser comes to <c>/oauth/authorize</c>. When the client and the address to go back to are right, the request
/// is checked, kept (<c>oauth_authorization_requests</c>, by the hash of a random id) and the browser is sent on to the
/// app's consent page (<c>/connect?request={id}</c>). The first signed-in person who opens it has it — for anyone else
/// it is not found —; they see who asks and what for (<see cref="GetAsync"/>), and decide once: allow what they choose,
/// never more than the admin gave them, or refuse (<see cref="ApproveAsync"/>, <see cref="DenyAsync"/>); a second
/// decision is refused. Then the browser goes back to the client with a code (or a refusal), the issuer named
/// (RFC 9207). When the client or the address is wrong, the browser is not sent to it at all: the consent page shows
/// the error. Expired requests are purged.
/// </para>
/// </summary>
public sealed partial class AuthorizationRequests(
    ShelfDbContext db,
    ClientRegistry clients,
    McpAddresses addresses,
    PermissionCatalog permissions,
    IOptionsMonitor<OAuthOptions> options,
    TimeProvider time)
{
    /// <summary>
    /// Where the browser goes from the authorization endpoint.
    /// </summary>
    public async Task<string> AuthorizeAsync(IQueryCollection query, CancellationToken cancellationToken)
    {
        string? One(string name) => query[name] is { Count: 1 } x ? x[0] : null;

        var clientId = One("client_id");
        var client = string.IsNullOrEmpty(clientId) ? null : await clients.ResolveForAuthorizationAsync(clientId, cancellationToken);
        if (client == null)
            return addresses.ConsentError(McpErrorCodes.UnknownClient);

        var redirectUri = One("redirect_uri");
        if (redirectUri == null || !ClientRegistry.Matches(client, redirectUri))
            return addresses.ConsentError(McpErrorCodes.RedirectMismatch);

        var state = One("state");
        string Refuse(string error, string description) => Back(redirectUri, state, ("error", error), ("error_description", description));

        if (One("response_type") != "code")
            return Refuse("unsupported_response_type", "response_type must be code.");
        if (One("code_challenge_method") != "S256" || One("code_challenge") is not { } challenge || !PkceChallengeFormat().IsMatch(challenge))
            return Refuse("invalid_request", "PKCE is required: code_challenge (S256) and code_challenge_method=S256.");
        if (query["resource"].Count > 1 || (One("resource") is { } resource && !SameResource(resource)))
            return Refuse("invalid_target", $"This server issues tokens for {addresses.Resource} only.");
        if (state?.Length > 2000)
            return Refuse("invalid_request", "state is too long.");

        var now = time.GetUtcNow();
        await db.OAuthAuthorizationRequests.Where(x => x.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);

        var id = AuthTokens.NewToken();
        db.OAuthAuthorizationRequests.Add(new()
        {
            IdHash = AuthTokens.Hash(id),
            ClientId = client.ClientId,
            RedirectUri = redirectUri,
            State = state,
            CodeChallenge = challenge,
            Scopes = McpPermissions.Of((One("scope") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList(),
            CreatedAt = now,
            ExpiresAt = now + options.CurrentValue.RequestLifetime,
        });
        await db.SaveChangesAsync(cancellationToken);
        return addresses.ConsentPage(id);
    }

    /// <summary>
    /// What the consent page shows; the first person who opens a request has it.
    /// </summary>
    /// <exception cref="ShelfException">404 <c>authorization_request_invalid</c>: not known, expired, another person's;
    /// 409 <c>authorization_request_decided</c>.</exception>
    public async Task<AuthorizationRequestView> GetAsync(ClaimsPrincipal user, string requestId, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;
        var request = await TakeAsync(me, requestId, cancellationToken);
        if (request.DecidedAt != null)
            throw ShelfException.Conflict(McpErrorCodes.RequestDecided);

        var allowed = await db.OAuthGrants.Where(x => x.AccountId == me && x.ClientId == request.ClientId).Select(x => x.Permissions).SingleOrDefaultAsync(cancellationToken);
        return new()
        {
            Client = Info(request.Client, request.RedirectUri),
            Requested = request.Scopes,
            Grantable = McpPermissions.Of(permissions.InForce(user)),
            Allowed = allowed == null ? null : McpPermissions.Of(allowed),
            ExpiresAt = request.ExpiresAt,
        };
    }

    /// <summary>
    /// The person allows the client what they chose: the connection (grant) is made or its permissions replaced, and a
    /// code is given to the client.
    /// </summary>
    public async Task<AuthorizationDecision> ApproveAsync(ClaimsPrincipal user, string requestId, IReadOnlyList<string> chosen, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;

        // another person's request is not found, whatever they choose; what is chosen is checked before the decision
        await TakeAsync(me, requestId, cancellationToken);
        var granted = Grantable(user, chosen, permissions);

        // decided, and the grant and code saved, together: a failure leaves the request undecided
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var request = await DecideAsync(me, requestId, cancellationToken);
        var now = time.GetUtcNow();

        // One grant per person and client (RFC 6749's and common practice's model; the shelf keeps it as one row): what
        // a token may do is read from it on every call. So a narrower consent narrows every live token of the client at
        // once; a wider one would widen tokens the person did not just approve on (another device, another session of
        // the client) — those are revoked, and only the token of this consent has the wider rights.
        var grant = await db.OAuthGrants.SingleOrDefaultAsync(x => x.AccountId == me && x.ClientId == request.ClientId, cancellationToken);
        if (grant == null)
        {
            grant = new() { Id = Guid.NewGuid(), AccountId = me, ClientId = request.ClientId, CreatedAt = now };
            db.OAuthGrants.Add(grant);
        }
        else if (granted.Except(grant.Permissions, StringComparer.Ordinal).Any())
        {
            await db.OAuthTokens.Where(x => x.GrantId == grant.Id).ExecuteDeleteAsync(cancellationToken);
            await db.OAuthAuthorizationCodes.Where(x => x.GrantId == grant.Id).ExecuteDeleteAsync(cancellationToken);
        }

        grant.Permissions = granted.ToList();
        grant.UpdatedAt = now;

        var code = AuthTokens.NewToken();
        db.OAuthAuthorizationCodes.Add(new()
        {
            CodeHash = AuthTokens.Hash(code),
            GrantId = grant.Id,
            FamilyId = Guid.NewGuid(),
            RedirectUri = request.RedirectUri,
            CodeChallenge = request.CodeChallenge,
            CodeChallengeMethod = "S256",
            Resource = addresses.Resource,
            CreatedAt = now,
            ExpiresAt = now + options.CurrentValue.CodeLifetime,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new() { RedirectUrl = Back(request.RedirectUri, request.State, ("code", code)) };
    }

    public async Task<AuthorizationDecision> DenyAsync(ClaimsPrincipal user, string requestId, CancellationToken cancellationToken)
    {
        var request = await DecideAsync(user.GetAccountId()!.Value, requestId, cancellationToken);
        return new() { RedirectUrl = Back(request.RedirectUri, request.State, ("error", "access_denied"), ("error_description", "The person did not allow it.")) };
    }

    /// <summary>
    /// The request for this person: bound to them if nobody had it yet.
    /// </summary>
    private async Task<OAuthAuthorizationRequest> TakeAsync(Guid me, string requestId, CancellationToken cancellationToken)
    {
        var hash = AuthTokens.Hash(requestId ?? string.Empty);
        var now = time.GetUtcNow();
        await db.OAuthAuthorizationRequests
            .Where(x => x.IdHash == hash && x.AccountId == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.AccountId, me), cancellationToken);

        return await db.OAuthAuthorizationRequests.AsNoTracking().Include(x => x.Client)
                   .SingleOrDefaultAsync(x => x.IdHash == hash && x.AccountId == me && x.ExpiresAt > now, cancellationToken) is { } request
               && ClientRegistry.Matches(request.Client, request.RedirectUri)
            ? request
            : throw ShelfException.NotFound(McpErrorCodes.RequestInvalid);
    }

    /// <summary>
    /// The request decided now, once: a second decision is refused.
    /// </summary>
    private async Task<OAuthAuthorizationRequest> DecideAsync(Guid me, string requestId, CancellationToken cancellationToken)
    {
        var request = await TakeAsync(me, requestId, cancellationToken);
        var now = time.GetUtcNow();
        var decided = await db.OAuthAuthorizationRequests
            .Where(x => x.IdHash == request.IdHash && x.AccountId == me && x.DecidedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.DecidedAt, now), cancellationToken);
        return decided == 1 ? request : throw ShelfException.Conflict(McpErrorCodes.RequestDecided);
    }

    /// <summary>
    /// The permissions chosen for a client or a key, checked: MCP permissions the person has now, at least one.
    /// </summary>
    /// <exception cref="ShelfException">400 <c>nothing_chosen</c>, <c>permission_not_grantable</c>.</exception>
    public static IReadOnlyList<string> Grantable(ClaimsPrincipal user, IReadOnlyList<string>? chosen, PermissionCatalog permissions)
    {
        if (chosen is not { Count: > 0 })
            throw ShelfException.BadRequest(McpErrorCodes.NothingChosen);

        var has = permissions.InForce(user);
        foreach (var permission in chosen)
        {
            if (permission == null || !McpPermissions.IsMcp(permission) || !has.Contains(permission))
                throw ShelfException.BadRequest(McpErrorCodes.NotGrantable, new Dictionary<string, string> { ["permission"] = permission ?? string.Empty });
        }

        return McpPermissions.Of(chosen);
    }

    public static McpClientInfo Info(OAuthClient client, string redirectUri) => new()
    {
        ClientId = client.ClientId,
        Kind = ClientRegistry.KindOf(client.ClientId),
        Name = ClientRegistry.NameOf(client),
        Uri = ClientRegistry.ShownUriOf(client),
        ClientIdHost = ClientRegistry.IdHostOf(client),
        RedirectHost = new Uri(redirectUri).IdnHost,
        LocalOnly = client.RedirectUris.All(x => ClientRegistry.IsLoopback(new Uri(x).Host)),
    };
    /// <summary>
    /// The address the browser goes back to the client by: its redirect address with the answer and the issuer.
    /// </summary>
    private string Back(string redirectUri, string? state, params (string Name, string Value)[] answer)
    {
        var parameters = answer.ToList();
        if (state != null)
            parameters.Add(("state", state));
        parameters.Add(("iss", addresses.Issuer));
        var query = string.Join('&', parameters.Select(x => $"{Uri.EscapeDataString(x.Name)}={Uri.EscapeDataString(x.Value)}"));
        return redirectUri + (redirectUri.Contains('?') ? '&' : '?') + query;
    }

    /// <summary>
    /// The resource a client names, compared with this server's: scheme and host without regard to case, a slash at
    /// the end ignored.
    /// </summary>
    public bool SameResource(string resource)
    {
        if (!Uri.TryCreate(resource.TrimEnd('/'), UriKind.Absolute, out var given) || given.Fragment.Length > 0 || given.Query.Length > 0)
            return false;

        var own = new Uri(addresses.Resource);
        return given.Scheme == own.Scheme && given.Host == own.Host && given.Port == own.Port && given.AbsolutePath == own.AbsolutePath;
    }

    /// <summary>
    /// A PKCE challenge of S256: base64url of 32 bytes.
    /// </summary>
    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex PkceChallengeFormat();
}
