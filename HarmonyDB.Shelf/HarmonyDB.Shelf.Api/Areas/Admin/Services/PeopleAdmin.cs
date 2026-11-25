using System.Text.Json;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Admin.Services;

/// <summary>
/// The people of the shelf as the admin site sees them, and their permissions: read, set whole, and each change written
/// to the audit log. A change takes effect at the person's next request (every sign-in reads the permissions anew).
/// </summary>
public sealed class PeopleAdmin(ShelfDbContext db, PermissionCatalog catalog, IOptionsMonitor<PermissionsOptions> options, TimeProvider time)
{
    /// <summary>
    /// The audit log's action of a change of a person's permissions.
    /// </summary>
    public const string PermissionsChangedAction = "permissions_changed";

    private const int MaxPage = 200;

    private const int MaxChanges = 500;

    /// <param name="search">A part of a name, user name or e-mail at a provider; a provider's id; an account's id.</param>
    /// <param name="permission">Only the people who have this permission.</param>
    /// <param name="anyPermission">Only the people who have some permission.</param>
    public async Task<AdminPeoplePage> ListAsync(string? search, string? permission, bool anyPermission, int skip, int take, CancellationToken cancellationToken)
    {
        var people = db.Accounts.AsNoTracking().Where(x => x.DeletedAt == null);

        if (!string.IsNullOrEmpty(permission))
            people = people.Where(x => x.Permissions.Any(p => p.Permission == permission));
        if (anyPermission)
            people = people.Where(x => x.Permissions.Any());

        var text = search?.Trim().TrimStart('@');
        if (!string.IsNullOrEmpty(text))
        {
            if (Guid.TryParse(text, out var id))
            {
                people = people.Where(x => x.Id == id);
            }
            else
            {
                var pattern = $"%{EscapeLike(text)}%";
                people = people.Where(x => EF.Functions.ILike(x.DisplayName, pattern, @"\")
                    || x.Logins.Any(l => l.Subject == text
                        || EF.Functions.ILike(l.DisplayName!, pattern, @"\")
                        || EF.Functions.ILike(l.Username!, pattern, @"\")
                        || EF.Functions.ILike(l.Email!, pattern, @"\")));
            }
        }

        var total = await people.CountAsync(cancellationToken);
        var page = await Summaries(people
                .OrderByDescending(x => x.LastSeenAt.HasValue)
                .ThenByDescending(x => x.LastSeenAt)
                .ThenByDescending(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .Skip(Math.Max(skip, 0))
                .Take(Math.Clamp(take, 1, MaxPage)))
            .ToListAsync(cancellationToken);

        return new() { Total = total, People = page.Select(x => x.ToSummary()).ToList() };
    }

    public async Task<AdminPerson> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await Summaries(db.Accounts.AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync(cancellationToken)
                  ?? throw ShelfException.NotFound(AdminErrorCodes.NoSuchPerson);

        var permissions = await db.AccountPermissions.AsNoTracking().Where(x => x.AccountId == id)
            .OrderBy(x => x.GrantedAt).ThenBy(x => x.Permission)
            .ToListAsync(cancellationToken);

        var links = await db.MusicLinks.AsNoTracking().Where(x => x.AccountId == id)
            .OrderBy(x => x.Service)
            .Select(x => new { x.Service, x.ExternalName, x.CreatedAt, x.LastUsedAt })
            .ToListAsync(cancellationToken);

        return new()
        {
            Summary = row.ToSummary(),
            Permissions = permissions.Select(x => new AdminGrantedPermission
            {
                Permission = x.Permission,
                IsKnown = catalog.IsKnown(x.Permission),
                GrantedAt = x.GrantedAt,
                GrantedBy = x.GrantedBy,
            }).ToList(),
            MusicLinks = links.Select(x => new AdminMusicLink
            {
                Service = ServiceName(x.Service),
                ExternalName = x.ExternalName,
                CreatedAt = x.CreatedAt,
                LastUsedAt = x.LastUsedAt,
            }).ToList(),
        };
    }

    public AdminPermissionCatalog GetCatalog()
    {
        var all = catalog.All;
        return new()
        {
            BuiltIn = all.Where(x => x.IsBuiltIn).Select(x => x.Key).ToList(),
            Custom = all.Where(x => !x.IsBuiltIn).Select(x => new AdminCustomPermission { Key = x.Key, Label = x.Label! }).ToList(),
            GuestDefaults = options.CurrentValue.Defaults.Guest,
            NewAccountDefaults = options.CurrentValue.Defaults.NewAccount,
        };
    }

    /// <summary>
    /// Sets the person's permissions whole, as the internal client <paramref name="client"/>; the change (if any) is
    /// written to the audit log in the same transaction.
    /// </summary>
    public async Task<AdminPermissionChange> SetPermissionsAsync(Guid id, AdminSetPermissionsRequest request, string client, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var wanted = request.Permissions.ToHashSet(StringComparer.Ordinal);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // The account's row locked: two changes of one person's permissions go one after the other.
        var account = (await db.Accounts.FromSql($"SELECT * FROM accounts WHERE id = {id} FOR UPDATE")
                          .AsNoTracking().ToListAsync(cancellationToken)).SingleOrDefault()
                      ?? throw ShelfException.NotFound(AdminErrorCodes.NoSuchPerson);
        if (account.DeletedAt != null)
            throw ShelfException.Conflict(AdminErrorCodes.PersonDeleted);

        var stored = await db.AccountPermissions.Where(x => x.AccountId == id).ToListAsync(cancellationToken);
        var current = stored.Select(x => x.Permission).ToHashSet(StringComparer.Ordinal);

        if (!current.SetEquals(request.Expected))
            throw ShelfException.Conflict(AdminErrorCodes.PermissionsChangedMeanwhile);

        // An unknown permission may stay with a person who has it, but is not given.
        var unknown = wanted.Where(x => !current.Contains(x) && !catalog.IsKnown(x)).Order(StringComparer.Ordinal).FirstOrDefault();
        if (unknown != null)
            throw ShelfException.BadRequest(AdminErrorCodes.UnknownPermission, new Dictionary<string, string> { ["permission"] = unknown });

        var added = wanted.Where(x => !current.Contains(x)).Order(StringComparer.Ordinal).ToList();
        var removed = current.Where(x => !wanted.Contains(x)).Order(StringComparer.Ordinal).ToList();

        var change = new AdminPermissionChange
        {
            Id = 0,
            At = now,
            AccountId = id,
            DisplayName = account.DisplayName,
            Actor = Actor(client),
            Added = added,
            Removed = removed,
        };
        if (added.Count == 0 && removed.Count == 0)
            return change;

        // Not every writer of permissions takes the account's lock (the import tool does not): a permission given
        // meanwhile by another is kept as it is, and one taken away meanwhile is simply gone.
        await db.AccountPermissions.Where(x => x.AccountId == id && removed.Contains(x.Permission))
            .ExecuteDeleteAsync(cancellationToken);
        foreach (var permission in added)
        {
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO account_permissions (account_id, permission, granted_at, granted_by) VALUES ({id}, {permission}, {now}, {client}) ON CONFLICT DO NOTHING",
                cancellationToken);
        }

        var entry = new AuditEntry
        {
            At = now,
            AccountId = id,
            Actor = change.Actor,
            Action = PermissionsChangedAction,
            Data = JsonSerializer.Serialize(new ChangeData { Added = added, Removed = removed }, ChangeData.Json),
        };
        db.AuditLog.Add(entry);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return change with { Id = entry.Id };
    }

    /// <summary>
    /// The latest changes of permissions, newest first: everyone's, or one person's.
    /// </summary>
    public async Task<IReadOnlyList<AdminPermissionChange>> GetChangesAsync(Guid? accountId, int take, CancellationToken cancellationToken)
    {
        var entries = db.AuditLog.AsNoTracking().Where(x => x.Action == PermissionsChangedAction);
        if (accountId != null)
            entries = entries.Where(x => x.AccountId == accountId);

        var rows = await entries
            .OrderByDescending(x => x.Id)
            .Take(Math.Clamp(take, 1, MaxChanges))
            .Select(x => new
            {
                x.Id,
                x.At,
                x.AccountId,
                x.Actor,
                x.Data,
                DisplayName = db.Accounts.Where(a => a.Id == x.AccountId && a.DeletedAt == null).Select(a => a.DisplayName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x =>
        {
            var data = x.Data == null ? null : JsonSerializer.Deserialize<ChangeData>(x.Data, ChangeData.Json);
            return new AdminPermissionChange
            {
                Id = x.Id,
                At = x.At,
                AccountId = x.AccountId!.Value,
                DisplayName = x.DisplayName,
                Actor = x.Actor,
                Added = data?.Added ?? [],
                Removed = data?.Removed ?? [],
            };
        }).ToList();
    }

    /// <summary>
    /// How an internal client is named as the actor in the audit log.
    /// </summary>
    public static string Actor(string client) => $"internal:{client}";

    private IQueryable<SummaryRow> Summaries(IQueryable<Account> accounts)
    {
        var now = time.GetUtcNow();
        return accounts
            .Select(x => new SummaryRow
            {
                Id = x.Id,
                DisplayName = x.DisplayName,
                PictureUrl = x.PictureUrl,
                SystemName = x.SystemName,
                CreatedAt = x.CreatedAt,
                LastSeenAt = x.LastSeenAt,
                DeletedAt = x.DeletedAt,
                Logins = x.Logins.OrderBy(l => l.CreatedAt).Select(l => new AdminLogin
                {
                    Provider = l.Provider,
                    Subject = l.Subject,
                    DisplayName = l.DisplayName,
                    Username = l.Username,
                    Email = l.Email,
                    CreatedAt = l.CreatedAt,
                    LastSignInAt = l.LastSignInAt,
                }).ToList(),
                MusicServices = db.MusicLinks.Where(m => m.AccountId == x.Id).OrderBy(m => m.Service).Select(m => m.Service).ToList(),
                Permissions = x.Permissions.OrderBy(p => p.Permission).Select(p => p.Permission).ToList(),
                Playlists = db.PlaylistMembers.Count(m => m.AccountId == x.Id),
                Marks = db.Marks.Count(m => m.AccountId == x.Id),
                Sessions = db.Sessions.Count(s => s.AccountId == x.Id && s.ExpiresAt > now),
                Clients = db.OAuthGrants.Count(g => g.AccountId == x.Id)
                          + db.PersonalKeys.Count(k => k.AccountId == x.Id && (k.ExpiresAt == null || k.ExpiresAt > now)),
            })
            .AsSplitQuery();
    }

    private static string ServiceName(MusicService service) => service switch
    {
        MusicService.Yandex => "yandex",
        MusicService.Youtube => "youtube",
        _ => throw new ArgumentOutOfRangeException(nameof(service), service, null),
    };

    private static string EscapeLike(string text) => text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    private sealed class SummaryRow
    {
        public Guid Id { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string? PictureUrl { get; init; }
        public string? SystemName { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? LastSeenAt { get; init; }
        public DateTimeOffset? DeletedAt { get; init; }
        public List<AdminLogin> Logins { get; init; } = [];
        public List<MusicService> MusicServices { get; init; } = [];
        public List<string> Permissions { get; init; } = [];
        public int Playlists { get; init; }
        public int Marks { get; init; }
        public int Sessions { get; init; }
        public int Clients { get; init; }

        public AdminPersonSummary ToSummary() => new()
        {
            Id = Id,
            DisplayName = DisplayName,
            PictureUrl = PictureUrl,
            SystemName = SystemName,
            CreatedAt = CreatedAt,
            LastSeenAt = LastSeenAt,
            DeletedAt = DeletedAt,
            Logins = Logins,
            MusicLinks = MusicServices.Select(ServiceName).ToList(),
            Permissions = Permissions,
            Counts = new() { Playlists = Playlists, Marks = Marks, Sessions = Sessions, Clients = Clients },
        };
    }

    /// <summary>
    /// The audit entry's data of a change of permissions.
    /// </summary>
    private sealed class ChangeData
    {
        public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public List<string> Added { get; init; } = [];
        public List<string> Removed { get; init; } = [];
    }
}
