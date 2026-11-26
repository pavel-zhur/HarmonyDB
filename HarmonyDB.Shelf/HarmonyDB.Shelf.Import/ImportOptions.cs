namespace HarmonyDB.Shelf.Import;

/// <summary>
/// The import's settings (section <see cref="SectionName"/>). Who is who in the old data — the tenants to take as shared
/// libraries, the owner of the template playlist, the titles, the custom permissions — is said here, never in code.
/// </summary>
public sealed class ImportOptions
{
    public const string SectionName = "Import";

    /// <summary>
    /// The tenant whose default-template versions are the template set.
    /// </summary>
    public int TemplateTenantId { get; set; }

    /// <summary>
    /// The Telegram user id of the person who owns the public playlist of the templates (and whose "here" keys in it are
    /// the templates' recommended keys).
    /// </summary>
    public long TemplateOwnerTelegramId { get; set; }

    public string TemplatePlaylistTitle { get; set; } = string.Empty;

    /// <summary>
    /// The title of a person's own private playlist: what they had beyond the templates.
    /// </summary>
    public string PersonalPlaylistTitle { get; set; } = string.Empty;

    /// <summary>
    /// The tenants of more than one person that are taken. A tenant of more than one person not listed here is left
    /// untouched, with its people, and reported.
    /// </summary>
    public List<SharedTenantOptions> SharedTenants { get; set; } = [];

    /// <summary>
    /// The tenants decided not to be imported: nothing of them is taken, nor their people (who get an account when they
    /// sign in themselves), except the people named; the report counts what is left behind.
    /// </summary>
    public List<NotImportedTenantOptions> NotImportedTenants { get; set; } = [];

    /// <summary>
    /// The custom permissions given to the people of the tenants with a tag: tag → keys.
    /// </summary>
    public Dictionary<string, List<string>> PermissionsByTenantTag { get; set; } = new();

    /// <summary>
    /// The permissions given to every person imported, besides what they had: built-in ones by their names, custom ones
    /// by their keys.
    /// </summary>
    public List<string> PermissionsForEveryone { get; set; } = [];

    public void Validate()
    {
        var failures = new List<string>();
        if (TemplateTenantId <= 0) failures.Add($"{SectionName}:{nameof(TemplateTenantId)} is not set.");
        if (TemplateOwnerTelegramId <= 0) failures.Add($"{SectionName}:{nameof(TemplateOwnerTelegramId)} is not set.");
        if (string.IsNullOrWhiteSpace(TemplatePlaylistTitle)) failures.Add($"{SectionName}:{nameof(TemplatePlaylistTitle)} is empty.");
        if (string.IsNullOrWhiteSpace(PersonalPlaylistTitle)) failures.Add($"{SectionName}:{nameof(PersonalPlaylistTitle)} is empty.");
        foreach (var tenant in SharedTenants)
        {
            if (tenant.TenantId <= 0) failures.Add($"{SectionName}:{nameof(SharedTenants)}: a tenant id is not set.");
            if (string.IsNullOrWhiteSpace(tenant.Title)) failures.Add($"{SectionName}:{nameof(SharedTenants)}:{tenant.TenantId}: the title is empty.");
            if (tenant.OwnerTelegramId <= 0) failures.Add($"{SectionName}:{nameof(SharedTenants)}:{tenant.TenantId}: the owner is not set.");

        }

        if (SharedTenants.GroupBy(x => x.TenantId).Any(x => x.Count() > 1)) failures.Add($"{SectionName}:{nameof(SharedTenants)}: a tenant is listed twice.");
        if (NotImportedTenants.Select(x => x.TenantId).Intersect(SharedTenants.Select(x => x.TenantId)).Any()) failures.Add($"{SectionName}:{nameof(NotImportedTenants)}: a tenant is also shared.");
        if (NotImportedTenants.Any(x => x.TenantId == TemplateTenantId)) failures.Add($"{SectionName}:{nameof(NotImportedTenants)}: the template tenant is not imported.");
        if (NotImportedTenants.GroupBy(x => x.TenantId).Any(x => x.Count() > 1)) failures.Add($"{SectionName}:{nameof(NotImportedTenants)}: a tenant is listed twice.");
        if (PermissionsByTenantTag.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value.Any(string.IsNullOrWhiteSpace)))
            failures.Add($"{SectionName}:{nameof(PermissionsByTenantTag)} has an empty tag or key.");

        if (PermissionsForEveryone.Any(string.IsNullOrWhiteSpace))
            failures.Add($"{SectionName}:{nameof(PermissionsForEveryone)} has an empty one.");

        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
    }
}

/// <summary>
/// A tenant of several people taken as one private playlist of its people.
/// </summary>
public sealed class SharedTenantOptions
{
    public int TenantId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The Telegram user id of the person who owns the playlist; the others are its members.
    /// </summary>
    public long OwnerTelegramId { get; set; }
}

/// <summary>
/// A tenant that is not imported, but for the people named: each of them gets an account, and a private playlist of
/// their own with the songs of this tenant that they added themselves (those whose chords are all in
/// <see cref="BeyondTenantId"/>'s songs left out), with their versions, and their likes, keys and comments on them;
/// their own chords and the chords they opened. Nothing else of the tenant.
/// </summary>
public sealed class NotImportedTenantOptions
{
    public int TenantId { get; set; }

    public List<long> ImportPeople { get; set; } = [];

    public int? BeyondTenantId { get; set; }
}

/// <summary>
/// The index's relay of the source contract, version 2 (section <see cref="SectionName"/>), where the import resolves
/// the old addresses as an internal client.
/// </summary>
public sealed class RelayOptions
{
    public const string SectionName = "Relay";

    /// <summary>
    /// The base address of the relay's operations, ending with a slash.
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// The import's internal key at the relay.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The keys the import calls with (<c>Caller.Keys</c>): those under which every stored item is open and which make
    /// no source fetch what it does not have.
    /// </summary>
    public List<string> CallerKeys { get; set; } = [];

    /// <summary>
    /// The keys of the second pass (<c>second-pass</c>): those under which the sources may go out for an address they
    /// do not have.
    /// </summary>
    public List<string> SecondPassCallerKeys { get; set; } = [];

    /// <summary>
    /// The pause of the second pass between two addresses: it asks one address at a time, slowly.
    /// </summary>
    public TimeSpan SecondPassPause { get; set; } = TimeSpan.FromSeconds(20);
    /// <summary>
    /// After how many failures in a row from one site the second pass leaves that site alone until its next start.
    /// </summary>
    public int SecondPassSiteFailures { get; set; } = 3;

    /// <summary>
    /// How many addresses go in one call.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
}
