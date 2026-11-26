namespace HarmonyDB.Shelf.Model.Admin;

// The internal API of the admin site (scope "shelf.admin"): the people of the shelf and their permissions. Not part of
// the browser's API.

/// <summary>
/// A page of people, newest activity first.
/// </summary>
public record AdminPeoplePage
{
    /// <summary>
    /// How many people match the search in all.
    /// </summary>
    public required int Total { get; init; }

    public required IReadOnlyList<AdminPersonSummary> People { get; init; }
}

/// <summary>
/// A person as the list shows them: enough to recognise the account and see what it may do.
/// </summary>
public record AdminPersonSummary
{
    public required Guid Id { get; init; }

    public required string DisplayName { get; init; }

    public string? PictureUrl { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastSeenAt { get; init; }

    /// <summary>
    /// When the person deleted their profile (or it was merged into another); null while it lives.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; init; }

    public required IReadOnlyList<AdminLogin> Logins { get; init; }

    /// <summary>
    /// The music services the person has linked an account of: "yandex", "youtube".
    /// </summary>
    public required IReadOnlyList<string> MusicLinks { get; init; }

    /// <summary>
    /// The permissions the person has, as stored (custom keys included, also ones no longer in the configuration).
    /// </summary>
    public required IReadOnlyList<string> Permissions { get; init; }

    public required AdminPersonCounts Counts { get; init; }
}

/// <summary>
/// One sign-in of the person: who they are at the provider. No tokens.
/// </summary>
public record AdminLogin
{
    /// <summary>
    /// "telegram", "google", "yandex", "dev".
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// The provider's id of the identity.
    /// </summary>
    public required string Subject { get; init; }

    public string? DisplayName { get; init; }

    public string? Username { get; init; }

    public string? Email { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset LastSignInAt { get; init; }
}

/// <summary>
/// Numbers that help to recognise an account.
/// </summary>
public record AdminPersonCounts
{
    /// <summary>
    /// The playlists the person is a member of (their own included).
    /// </summary>
    public required int Playlists { get; init; }

    public required int Marks { get; init; }

    /// <summary>
    /// The browser sessions that have not ended.
    /// </summary>
    public required int Sessions { get; init; }

    /// <summary>
    /// The programs acting for the person: connected MCP clients and personal keys.
    /// </summary>
    public required int Clients { get; init; }
}

/// <summary>
/// One person in detail.
/// </summary>
public record AdminPerson
{
    public required AdminPersonSummary Summary { get; init; }

    /// <summary>
    /// The person's permissions as stored, with when and by whom each was given.
    /// </summary>
    public required IReadOnlyList<AdminGrantedPermission> Permissions { get; init; }

    public required IReadOnlyList<AdminMusicLink> MusicLinks { get; init; }
}

public record AdminGrantedPermission
{
    public required string Permission { get; init; }

    /// <summary>
    /// Whether the shelf knows it: built-in, or custom in the configuration now. An unknown one is kept until it is
    /// taken away.
    /// </summary>
    public required bool IsKnown { get; init; }

    public required DateTimeOffset GrantedAt { get; init; }

    /// <summary>
    /// Who gave it: "defaults" (at the account's making), an internal client's name, a tool's.
    /// </summary>
    public string? GrantedBy { get; init; }
}

/// <summary>
/// A linked music account: which service and the name on it. Never what was copied.
/// </summary>
public record AdminMusicLink
{
    /// <summary>
    /// "yandex", "youtube".
    /// </summary>
    public required string Service { get; init; }

    public string? ExternalName { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastUsedAt { get; init; }
}

/// <summary>
/// The permissions there are, and the defaults; all from the code and the configuration of the shelf.
/// </summary>
public record AdminPermissionCatalog
{
    /// <summary>
    /// The built-in permissions, in their order.
    /// </summary>
    public required IReadOnlyList<string> BuiltIn { get; init; }

    /// <summary>
    /// The custom permissions of the configuration, in its order.
    /// </summary>
    public required IReadOnlyList<AdminCustomPermission> Custom { get; init; }

    /// <summary>
    /// What a guest has. From the configuration.
    /// </summary>
    public required IReadOnlyList<string> GuestDefaults { get; init; }

    /// <summary>
    /// What a new account is given when it is made. From the configuration.
    /// </summary>
    public required IReadOnlyList<string> NewAccountDefaults { get; init; }
}

public record AdminCustomPermission
{
    public required string Key { get; init; }

    public required string Label { get; init; }
}

/// <summary>
/// Sets a person's permissions whole.
/// </summary>
public record AdminSetPermissionsRequest
{
    /// <summary>
    /// The permissions the person is to have. Each is known to the shelf, or one the person has already (an unknown
    /// one can be kept or taken away, not given).
    /// </summary>
    public required IReadOnlyList<string> Permissions { get; init; }

    /// <summary>
    /// The permissions the admin saw: when the stored ones differ (changed meanwhile), nothing is changed and the answer
    /// is 409 <c>permissions_changed_meanwhile</c>.
    /// </summary>
    public required IReadOnlyList<string> Expected { get; init; }
}

/// <summary>
/// A change of a person's permissions, from the audit log.
/// </summary>
public record AdminPermissionChange
{
    /// <summary>
    /// The audit log entry; 0 in the answer to a setting that changed nothing (nothing was written).
    /// </summary>
    public required long Id { get; init; }

    public required DateTimeOffset At { get; init; }

    public required Guid AccountId { get; init; }

    /// <summary>
    /// The person's name now; null when the account is gone.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Who changed it: "internal:&lt;client name&gt;" for an internal client (the admin site), …
    /// </summary>
    public required string Actor { get; init; }

    public required IReadOnlyList<string> Added { get; init; }

    public required IReadOnlyList<string> Removed { get; init; }
}
