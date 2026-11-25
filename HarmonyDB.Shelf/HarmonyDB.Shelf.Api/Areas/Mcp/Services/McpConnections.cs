using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Auth.Services;
using HarmonyDB.Shelf.Api.Areas.OAuth.Services;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Mcp;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Areas.Mcp.Services;

/// <summary>
/// The person's assistants (connected clients) and keys for programs: listed with what each may do now, changed,
/// disconnected or revoked — at once: a disconnected client's tokens and a revoked key stop working with the next
/// request.
/// </summary>
public sealed class McpConnections(ShelfDbContext db, McpAddresses addresses, PermissionCatalog permissions, TimeProvider time)
{
    private const int MaxKeyName = 100;

    public McpOverview Overview(ClaimsPrincipal user) => new()
    {
        ServerUrl = addresses.Resource,
        Grantable = McpPermissions.Of(permissions.InForce(user)),
    };

    public async Task<IReadOnlyList<ConnectedClient>> ListClientsAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;
        var grants = await db.OAuthGrants.AsNoTracking().Include(x => x.Client)
            .Where(x => x.AccountId == me)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var given = permissions.InForce(user);
        return grants.Select(x => ToItem(x, given)).ToList();
    }

    public async Task<ConnectedClient> UpdateClientAsync(ClaimsPrincipal user, Guid id, IReadOnlyList<string> chosen, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;
        var grant = await db.OAuthGrants.Include(x => x.Client).SingleOrDefaultAsync(x => x.Id == id && x.AccountId == me, cancellationToken)
                    ?? throw ShelfException.NotFound(McpErrorCodes.NoSuchClient);
        grant.Permissions = AuthorizationRequests.Grantable(user, chosen, permissions).ToList();
        grant.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return ToItem(grant, permissions.InForce(user));
    }

    /// <summary>
    /// Disconnects the client: the grant goes, and with it every code and token it gave.
    /// </summary>
    public async Task DisconnectAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;
        if (await db.OAuthGrants.Where(x => x.Id == id && x.AccountId == me).ExecuteDeleteAsync(cancellationToken) == 0)
            throw ShelfException.NotFound(McpErrorCodes.NoSuchClient);
    }

    public async Task<IReadOnlyList<PersonalKeyItem>> ListKeysAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;
        var keys = await db.PersonalKeys.AsNoTracking().Where(x => x.AccountId == me).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        var given = permissions.InForce(user);
        return keys.Select(x => ToItem(x, given)).ToList();
    }

    /// <summary>
    /// Makes a key: random, shown in the answer once, kept as its hash.
    /// </summary>
    public async Task<CreatedPersonalKey> CreateKeyAsync(ClaimsPrincipal user, CreatePersonalKeyRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > MaxKeyName)
            throw ShelfException.BadRequest(McpErrorCodes.KeyNameRequired, new Dictionary<string, string> { ["max"] = MaxKeyName.ToString() });
        if (request.ExpiresInDays is < 1 or > 365)
            throw ShelfException.BadRequest(McpErrorCodes.KeyExpiryInvalid);

        var granted = AuthorizationRequests.Grantable(user, request.Permissions, permissions);
        var now = time.GetUtcNow();
        var key = McpBearerHandler.PersonalKeyPrefix + AuthTokens.NewToken();
        var row = new PersonalKey
        {
            Id = Guid.NewGuid(),
            AccountId = user.GetAccountId()!.Value,
            Name = name,
            KeyHash = AuthTokens.Hash(key),
            Prefix = key[..12],
            Permissions = granted.ToList(),
            CreatedAt = now,
            ExpiresAt = request.ExpiresInDays is { } days ? now.AddDays(days) : null,
        };
        db.PersonalKeys.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return new() { Key = key, Item = ToItem(row, permissions.InForce(user)) };
    }

    public async Task RevokeKeyAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;
        if (await db.PersonalKeys.Where(x => x.Id == id && x.AccountId == me).ExecuteDeleteAsync(cancellationToken) == 0)
            throw ShelfException.NotFound(McpErrorCodes.NoSuchKey);
    }

    private static ConnectedClient ToItem(OAuthGrant grant, IReadOnlySet<string> given) => new()
    {
        Id = grant.Id,
        Kind = ClientRegistry.KindOf(grant.ClientId),
        Name = ClientRegistry.NameOf(grant.Client),
        Uri = grant.Client.ClientUri,
        ClientIdHost = ClientRegistry.IdHostOf(grant.Client),
        RedirectHosts = grant.Client.RedirectUris.Select(x => new Uri(x).Host).Distinct().ToList(),
        ConnectedAt = grant.CreatedAt,
        LastUsedAt = grant.LastUsedAt,
        Allowed = McpPermissions.Of(grant.Permissions),
        InForce = McpPermissions.Of(grant.Permissions.Where(given.Contains)),
    };

    private static PersonalKeyItem ToItem(PersonalKey key, IReadOnlySet<string> given) => new()
    {
        Id = key.Id,
        Name = key.Name,
        Prefix = key.Prefix,
        Allowed = McpPermissions.Of(key.Permissions),
        InForce = McpPermissions.Of(key.Permissions.Where(given.Contains)),
        CreatedAt = key.CreatedAt,
        ExpiresAt = key.ExpiresAt,
        LastUsedAt = key.LastUsedAt,
    };
}
