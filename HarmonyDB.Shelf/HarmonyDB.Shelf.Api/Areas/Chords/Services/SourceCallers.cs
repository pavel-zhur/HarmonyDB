using System.Security.Claims;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Source.Api.Model.V2;

namespace HarmonyDB.Shelf.Api.Areas.Chords.Services;

/// <summary>
/// Who a call to the sources is made for: the person's account (none for a guest) and their custom keys, exactly as
/// they are in force now. Nothing is added to them and nothing is made up.
/// </summary>
public sealed class SourceCallers(PermissionCatalog permissions)
{
    public Caller For(ClaimsPrincipal user) => new()
    {
        AccountId = user.GetAccountId(),
        Keys = permissions.CustomKeys(user).Order(StringComparer.Ordinal).ToList(),
    };
}
