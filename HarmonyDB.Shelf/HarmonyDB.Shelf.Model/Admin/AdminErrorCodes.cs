namespace HarmonyDB.Shelf.Model.Admin;

/// <summary>
/// The error codes of the admin site's internal API.
/// </summary>
public static class AdminErrorCodes
{
    /// <summary>404: no such account.</summary>
    public const string NoSuchPerson = "no_such_person";

    /// <summary>409: the account is deleted; its permissions are not changed.</summary>
    public const string PersonDeleted = "person_deleted";

    /// <summary>400: the permission is neither built-in nor in the configuration (details: <c>permission</c>).</summary>
    public const string UnknownPermission = "unknown_permission";

    /// <summary>409: the person's permissions changed since the admin looked; nothing was changed.</summary>
    public const string PermissionsChangedMeanwhile = "permissions_changed_meanwhile";
}
