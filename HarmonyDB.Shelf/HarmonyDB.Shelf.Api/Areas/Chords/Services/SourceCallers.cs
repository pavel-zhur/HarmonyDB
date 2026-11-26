using System.Security.Claims;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Source.Api.Model.V2;

namespace HarmonyDB.Shelf.Api.Areas.Chords.Services;

/// <summary>
/// Who a call to the sources is made for: the names the person is known by (none for a guest,
/// <see cref="HarmonyDB.Shelf.Database.AccountSubjects"/>) and their custom keys, exactly as they are in force now. Nothing is added to them and
/// nothing is made up.
/// </summary>
public sealed class SourceCallers(PermissionCatalog permissions, ShelfDbContext db)
{
    public Caller For(ClaimsPrincipal user) => new()
    {
        Subjects = user.GetSubjects(),
        Keys = permissions.CustomKeys(user).Order(StringComparer.Ordinal).ToList(),
    };

    /// <summary>
    /// For work done for a person outside their request (a job): their names as they are now, and the keys the work was
    /// given.
    /// </summary>
    public async Task<Caller> ForAccountAsync(Guid accountId, IReadOnlyList<string> keys, CancellationToken cancellationToken) => new()
    {
        Subjects = await AccountSubjects.LoadAsync(db, accountId, cancellationToken),
        Keys = keys,
    };

    /// <summary>
    /// For one who holds a link that carries access: the access it carries, and nobody's names.
    /// </summary>
    public static Caller Carrying(IReadOnlyList<string> keys) => new() { Subjects = [], Keys = keys };
}
