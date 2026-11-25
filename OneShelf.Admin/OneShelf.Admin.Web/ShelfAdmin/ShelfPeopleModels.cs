using HarmonyDB.Shelf.Model.Admin;

namespace OneShelf.Admin.Web.ShelfAdmin;

public sealed record ShelfPeopleModel(AdminPeoplePage Page, AdminPermissionCatalog Catalog, string? Search, string? Has, int Skip);

public sealed record ShelfPersonModel(AdminPerson Person, AdminPermissionCatalog Catalog, IReadOnlyList<AdminPermissionChange> Changes)
{
    public IReadOnlySet<string> Has { get; } = Person.Permissions.Select(x => x.Permission).ToHashSet(StringComparer.Ordinal);
}

public sealed record ShelfChangesModel(IReadOnlyList<AdminPermissionChange> Changes, AdminPermissionCatalog Catalog);

/// <summary>
/// What the pages need to name a permission: the short name of a built-in one, the label of a custom one, or the key.
/// </summary>
public static class ShelfPermissionNames
{
    public static string Short(this AdminPermissionCatalog catalog, string key)
    {
        if (ShelfPermissionGroups.Mcp.FirstOrDefault(x => x.Key == key) is { } mcp)
            return "MCP " + mcp.Short;
        if (ShelfPermissionGroups.App.FirstOrDefault(x => x.Key == key) is { } app)
            return "app " + app.Short;
        return catalog.Custom.FirstOrDefault(x => x.Key == key)?.Label ?? key;
    }

    /// <summary>
    /// The keys in the order of the pages: MCP, the app, other built-in, custom, unknown.
    /// </summary>
    public static IEnumerable<string> Ordered(this AdminPermissionCatalog catalog, IEnumerable<string> keys)
    {
        var order = ShelfPermissionGroups.Mcp.Concat(ShelfPermissionGroups.App).Select(x => x.Key)
            .Concat(catalog.BuiltIn)
            .Concat(catalog.Custom.Select(x => x.Key))
            .Distinct()
            .Select((key, index) => (key, index))
            .ToDictionary(x => x.key, x => x.index);
        return keys.OrderBy(x => order.GetValueOrDefault(x, int.MaxValue)).ThenBy(x => x, StringComparer.Ordinal);
    }

    public static bool IsCustom(this AdminPermissionCatalog catalog, string key) => catalog.Custom.Any(x => x.Key == key);

    public static bool IsKnown(this AdminPermissionCatalog catalog, string key) => catalog.BuiltIn.Contains(key) || catalog.IsCustom(key);

    /// <summary>
    /// A time as the pages show it.
    /// </summary>
    public static string Time(DateTimeOffset? at) => at?.UtcDateTime.ToString("yyyy-MM-dd HH:mm") ?? "—";
}

/// <summary>
/// Permissions as chips (<c>_Chips</c>): <paramref name="Kind"/> colours them all alike ("added", "removed"); null:
/// by what they are.
/// </summary>
public sealed record ShelfChips(IEnumerable<string> Keys, AdminPermissionCatalog Catalog, string? Kind = null);

/// <summary>
/// A list of changes of permissions (<c>_Changes</c>).
/// </summary>
public sealed record ShelfChangesList(IReadOnlyList<AdminPermissionChange> Changes, AdminPermissionCatalog Catalog, bool ShowPerson);
