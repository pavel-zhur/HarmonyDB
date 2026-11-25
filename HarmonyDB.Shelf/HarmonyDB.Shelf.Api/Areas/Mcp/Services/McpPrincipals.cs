using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.OAuth.Services;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Api.Areas.Mcp.Services;

/// <summary>
/// The delegated identity of a client acting for a person — by its connection (grant) or by a personal key — made as
/// the person's permissions and the client's allowance are now: their intersection is what is in force. Used for each
/// request to <c>/mcp</c>, and for a link a tool gave, which acts as its client still may.
/// </summary>
public sealed class McpPrincipals(ShelfDbContext db, PermissionCatalog permissions, TimeProvider time)
{
    public const string SchemeName = "McpBearer";

    /// <summary>
    /// The claim with the connection's id (<c>grant:{id}</c>) or the key's (<c>key:{id}</c>).
    /// </summary>
    public const string ClientClaimType = "shelf_mcp_client";

    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);

    public sealed record Found(ClaimsPrincipal Principal, Guid? GrantId, string? ClientId, Guid? KeyId, DateTimeOffset? LastUsedAt);

    /// <summary>
    /// The identity of a client by its reference (<see cref="ClientClaimType"/>), when it may still act.
    /// </summary>
    public async Task<ClaimsPrincipal?> ByClientAsync(string client, CancellationToken cancellationToken)
    {
        var (kind, id) = (client.Split(':')[0], Guid.TryParse(client.Split(':').ElementAtOrDefault(1), out var parsed) ? parsed : Guid.Empty);
        var now = time.GetUtcNow();
        return kind switch
        {
            "grant" => (await ByGrantAsync(db.OAuthGrants.Where(x => x.Id == id).Select(x => x.Id), cancellationToken))?.Principal,
            "key" => (await ByKeyAsync(db.PersonalKeys.Where(x => x.Id == id && (x.ExpiresAt == null || x.ExpiresAt > now)).Select(x => x.Id), cancellationToken))?.Principal,
            _ => null,
        };
    }

    public async Task<Found?> ByGrantAsync(IQueryable<Guid> grantIds, CancellationToken cancellationToken)
    {
        var grant = await db.OAuthGrants.AsNoTracking()
            .Where(x => grantIds.Contains(x.Id) && x.Account.DeletedAt == null)
            .Select(x => new { x.Id, x.ClientId, x.AccountId, x.Permissions, x.LastUsedAt, Given = x.Account.Permissions.Select(p => p.Permission).ToList() })
            .SingleOrDefaultAsync(cancellationToken);
        return grant == null ? null : new(Create(grant.AccountId, grant.Given, grant.Permissions, $"grant:{grant.Id}"), grant.Id, grant.ClientId, null, grant.LastUsedAt);
    }

    public async Task<Found?> ByKeyAsync(IQueryable<Guid> keyIds, CancellationToken cancellationToken)
    {
        var key = await db.PersonalKeys.AsNoTracking()
            .Where(x => keyIds.Contains(x.Id) && x.Account.DeletedAt == null)
            .Select(x => new { x.Id, x.AccountId, x.Permissions, x.LastUsedAt, Given = x.Account.Permissions.Select(p => p.Permission).ToList() })
            .SingleOrDefaultAsync(cancellationToken);
        return key == null ? null : new(Create(key.AccountId, key.Given, key.Permissions, $"key:{key.Id}"), null, null, key.Id, key.LastUsedAt);
    }

    /// <summary>
    /// Writes when the client was last used, at most once a minute.
    /// </summary>
    public async Task TouchAsync(Found found, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (found.LastUsedAt != null && now - found.LastUsedAt < TouchInterval)
            return;

        if (found.KeyId is { } keyId)
        {
            await db.PersonalKeys.Where(x => x.Id == keyId).ExecuteUpdateAsync(x => x.SetProperty(k => k.LastUsedAt, now), cancellationToken);
            return;
        }

        await db.OAuthGrants.Where(x => x.Id == found.GrantId).ExecuteUpdateAsync(x => x.SetProperty(g => g.LastUsedAt, now), cancellationToken);
        await db.OAuthClients.Where(x => x.ClientId == found.ClientId).ExecuteUpdateAsync(x => x.SetProperty(c => c.LastUsedAt, now), cancellationToken);
    }

    private ClaimsPrincipal Create(Guid accountId, IReadOnlyList<string> given, IReadOnlyList<string> allowed, string client)
    {
        var identity = ShelfIdentity.Create(SchemeName, accountId, given, McpPermissions.AllowedToClient(allowed, permissions));
        identity.AddClaim(new(ClientClaimType, client));
        return new(identity);
    }
}
