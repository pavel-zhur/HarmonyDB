using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Foundation.Permissions;

/// <summary>
/// The permissions there are (built-in and custom), and which are in force for a caller. The one place that answers
/// "may this caller …": the endpoints' check (<see cref="RequirePermissionAttribute"/>) and any other code ask it.
/// </summary>
public sealed class PermissionCatalog(IOptionsMonitor<PermissionsOptions> options)
{
    /// <summary>
    /// Every permission there is, built-in first; a custom one with its label from configuration.
    /// </summary>
    public IReadOnlyList<(string Key, string? Label, bool IsBuiltIn)> All
        => BuiltInPermissions.All
            .Select(x => (x, (string?)null, true))
            .Concat(options.CurrentValue.Custom.Select(x => (x.Key, (string?)x.Label, false)))
            .ToList();

    public bool IsKnown(string permission)
        => BuiltInPermissions.All.Contains(permission, StringComparer.Ordinal)
           || options.CurrentValue.Custom.Any(x => x.Key == permission);

    public bool IsCustom(string permission) => options.CurrentValue.Custom.Any(x => x.Key == permission);

    /// <summary>
    /// What a new account is given.
    /// </summary>
    public IReadOnlyList<string> NewAccountDefaults => options.CurrentValue.Defaults.NewAccount;

    /// <summary>
    /// The permissions in force for the caller: a signed-in person's (as their identity carries them), or a guest's
    /// defaults.
    /// </summary>
    public IReadOnlySet<string> InForce(ClaimsPrincipal caller)
        => caller.GetAccountId() == null
            ? options.CurrentValue.Defaults.Guest.ToHashSet(StringComparer.Ordinal)
            : caller.FindAll(ShelfIdentity.PermissionClaimType).Select(x => x.Value).ToHashSet(StringComparer.Ordinal);

    public bool Has(ClaimsPrincipal caller, string permission) => InForce(caller).Contains(permission);

    /// <summary>
    /// The caller's custom keys: what is sent to the sources as <c>Caller.Keys</c>.
    /// </summary>
    public IReadOnlyList<string> CustomKeys(ClaimsPrincipal caller)
    {
        var inForce = InForce(caller);
        return options.CurrentValue.Custom.Select(x => x.Key).Where(inForce.Contains).ToList();
    }
}
