using System.Security.Claims;

namespace HarmonyDB.Shelf.Api.Foundation.Permissions;

/// <summary>
/// How a signed-in person is seen by the API: an identity with the account's id and the permissions in force for this
/// request. Every way of signing in (the browser's session, an MCP client's token, a personal key) makes its identity
/// with <see cref="Create"/>, on every request, from the permissions the admin gave the person as they are now.
/// </summary>
public static class ShelfIdentity
{
    public const string AccountIdClaimType = "shelf_account";

    public const string PermissionClaimType = "shelf_permission";

    /// <summary>
    /// Present on the identity of a client acting for the person (an MCP client, a personal key).
    /// </summary>
    public const string DelegatedClaimType = "shelf_delegated";

    /// <param name="authenticationType">The authentication scheme that made it.</param>
    /// <param name="accountId">The person.</param>
    /// <param name="given">What the admin gave the person (their permissions, as stored now).</param>
    /// <param name="allowedToClient">For a client acting for the person (an MCP client, a personal key): what the person
    /// allowed it. The permissions in force are then those both in <paramref name="given"/> and in this, and the
    /// identity is marked delegated: it acts by those permissions only, never as the person's own session. Null for
    /// the person's own session.</param>
    public static ClaimsIdentity Create(
        string authenticationType,
        Guid accountId,
        IEnumerable<string> given,
        IReadOnlyCollection<string>? allowedToClient)
    {
        var inForce = allowedToClient == null
            ? given.Distinct(StringComparer.Ordinal)
            : given.Intersect(allowedToClient, StringComparer.Ordinal);

        var claims = inForce.Select(x => new Claim(PermissionClaimType, x)).Prepend(new(AccountIdClaimType, accountId.ToString()));
        if (allowedToClient != null)
            claims = claims.Append(new(DelegatedClaimType, "true"));

        return new(claims, authenticationType, AccountIdClaimType, null);
    }

    /// <summary>
    /// The account the caller is or acts for; null for a guest.
    /// </summary>
    public static Guid? GetAccountId(this ClaimsPrincipal principal)
        => principal.FindFirst(AccountIdClaimType) is { } claim ? Guid.Parse(claim.Value) : null;

    /// <summary>
    /// Whether the caller is a client acting for the person, not the person's own session.
    /// </summary>
    public static bool IsDelegated(this ClaimsPrincipal principal) => principal.HasClaim(x => x.Type == DelegatedClaimType);
}
