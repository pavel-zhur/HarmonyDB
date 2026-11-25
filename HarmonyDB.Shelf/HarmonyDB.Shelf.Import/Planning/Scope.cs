using HarmonyDB.Shelf.Import.Old;

namespace HarmonyDB.Shelf.Import.Planning;

/// <summary>
/// Who and what of the old shelf is taken: the people, the shared tenants with their people, each person's personal
/// tenants (their own, and those left with no user that the old shelf made for them), and what is left out.
/// </summary>
public sealed class Scope
{
    private Scope(
        IReadOnlyDictionary<long, OldUser> people,
        IReadOnlyDictionary<int, SharedTenantOptions> sharedTenants,
        IReadOnlyDictionary<int, long> personalTenants,
        IReadOnlyDictionary<long, IReadOnlySet<int>> ownSongsOnly,
        IReadOnlyList<Unmapped> leftOut)
    {
        People = people;
        SharedTenants = sharedTenants;
        PersonalTenants = personalTenants;
        OwnSongsOnly = ownSongsOnly;
        LeftOut = leftOut;
    }

    /// <summary>
    /// The people taken, by Telegram user id.
    /// </summary>
    public IReadOnlyDictionary<long, OldUser> People { get; }

    /// <summary>
    /// The configured shared tenants that exist.
    /// </summary>
    public IReadOnlyDictionary<int, SharedTenantOptions> SharedTenants { get; }

    /// <summary>
    /// Personal tenant → its person.
    /// </summary>
    public IReadOnlyDictionary<int, long> PersonalTenants { get; }

    /// <summary>
    /// The people named in a tenant not imported → the songs of it they take (those they added themselves); of
    /// everything else of theirs only their own chords and the chords they opened are taken.
    /// </summary>
    public IReadOnlyDictionary<long, IReadOnlySet<int>> OwnSongsOnly { get; }

    public IReadOnlyList<Unmapped> LeftOut { get; }

    public bool IsTaken(long userId) => People.ContainsKey(userId);

    /// <summary>
    /// Whether the person's likes, keys and comments of the song are taken.
    /// </summary>
    public bool IsTakenFor(long userId, int songId) => IsTaken(userId) && (!OwnSongsOnly.TryGetValue(userId, out var songs) || songs.Contains(songId));

    /// <summary>
    /// Whether a tenant's things are taken: a shared one configured, or a personal one of a person taken.
    /// </summary>
    public bool IsTakenTenant(int tenantId) => SharedTenants.ContainsKey(tenantId) || PersonalTenants.ContainsKey(tenantId);

    public IEnumerable<long> PeopleOf(int tenantId) => People.Values.Where(x => x.TenantId == tenantId).Select(x => x.Id).Order();

    public static Scope Of(OldShelf old, ImportOptions options)
    {
        var usersByTenant = old.Users.ToLookup(x => x.TenantId);
        var leftOut = new List<Unmapped>();
        var shared = options.SharedTenants
            .Where(x => old.Tenants.Any(t => t.Id == x.TenantId))
            .ToDictionary(x => x.TenantId);

        var people = new Dictionary<long, OldUser>();
        var personal = new Dictionary<int, long>();
        var ownSongsOnly = new Dictionary<long, IReadOnlySet<int>>();
        var notImported = options.NotImportedTenants.ToDictionary(x => x.TenantId);
        foreach (var tenant in old.Tenants)
        {
            var users = usersByTenant[tenant.Id].ToList();
            foreach (var user in users.Where(x => !OldRules.IsPerson(x.Id)))
                leftOut.Add(new("user", null, $"user {user.Id} ({user.Title}) of tenant {tenant.Id}", UnmappedReasons.NotAPerson));

            var persons = users.Where(x => OldRules.IsPerson(x.Id)).ToList();
            if (notImported.TryGetValue(tenant.Id, out var partial))
            {
                var named = persons.Where(x => partial.ImportPeople.Contains(x.Id)).ToList();
                var beyond = partial.BeyondTenantId is { } other
                    ? old.Songs.Where(x => x.TenantId == other).Select(x => x.Id).ToHashSet()
                    : [];
                var beyondUris = old.Versions.Where(x => beyond.Contains(x.SongId)).Select(x => x.Uri).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var person in named)
                {
                    people[person.Id] = person;
                    ownSongsOnly[person.Id] = old.Songs
                        .Where(x => x.TenantId == tenant.Id && x.CreatedByUserId == person.Id)
                        .Where(x => !old.Versions.Where(v => v.SongId == x.Id).All(v => beyondUris.Contains(v.Uri)))
                        .Select(x => x.Id)
                        .ToHashSet();
                }

                leftOut.Add(new("tenant", null, NotImported(old, tenant, persons.Select(x => x.Id).ToHashSet(), named.Count, ownSongsOnly), UnmappedReasons.TenantNotImported));
            }
            else if (shared.ContainsKey(tenant.Id))
            {
                foreach (var person in persons)
                    people[person.Id] = person;
            }
            else if (users.Count > 1)
            {
                leftOut.Add(new("tenant", null, $"tenant {tenant.Id}: {persons.Count} people, {old.Songs.Count(x => x.TenantId == tenant.Id)} songs", UnmappedReasons.TenantNotConfigured));
            }
            else if (persons.Count == 1)
            {
                people[persons[0].Id] = persons[0];
                personal[tenant.Id] = persons[0].Id;
            }
        }

        // the tenants left with no user: their person's, when that person is taken
        foreach (var tenant in old.Tenants.Where(x => !usersByTenant[x.Id].Any() && !shared.ContainsKey(x.Id)))
        {
            if (OldRules.PersonalTenantOf(tenant) is { } person && people.ContainsKey(person) && !ownSongsOnly.ContainsKey(person))
                personal[tenant.Id] = person;
            else if (old.Songs.Any(x => x.TenantId == tenant.Id) || old.Lists.Any(x => x.TenantId == tenant.Id))
                leftOut.Add(new("tenant", null, $"tenant {tenant.Id} ({tenant.PrivateDescription})", UnmappedReasons.OrphanTenantWithoutPerson));
        }

        return new(people, shared, personal, ownSongsOnly, leftOut);
    }

    /// <summary>
    /// What a tenant not imported leaves behind: its people, its songs and their likes, and what its people have in other
    /// tenants.
    /// </summary>
    private static string NotImported(OldShelf old, OldTenant tenant, IReadOnlySet<long> persons, int namedCount, IReadOnlyDictionary<long, IReadOnlySet<int>> taken)
    {
        var takenSongs = taken.Values.SelectMany(x => x).ToHashSet();
        var songs = old.Songs.Where(x => x.TenantId == tenant.Id).Select(x => x.Id).ToHashSet();
        var elsewhere = old.Songs.Where(x => x.TenantId != tenant.Id).Select(x => x.Id).ToHashSet();
        var orphans = old.Tenants.Where(x => OldRules.PersonalTenantOf(x) is { } p && persons.Contains(p) && !old.Users.Any(u => u.TenantId == x.Id)).ToList();
        return $"left behind of tenant {tenant.Id}: {persons.Count} people ({namedCount} named are taken, with {takenSongs.Count} songs they added), {songs.Count} songs ({old.Songs.Count(x => songs.Contains(x.Id) && x.Index >= OldRules.TemplateIndexLimit)} numbered beyond the templates), "
               + $"{old.Likes.Count(x => songs.Contains(x.SongId) && !(takenSongs.Contains(x.SongId) && taken.ContainsKey(x.UserId)))} likes and {old.Comments.Count(x => songs.Contains(x.SongId))} comments of its songs, {old.Lists.Count(x => x.TenantId == tenant.Id)} lists; "
               + $"its people elsewhere: {old.Likes.Count(x => persons.Contains(x.UserId) && elsewhere.Contains(x.SongId))} likes, "
               + $"{old.Comments.Count(x => persons.Contains(x.UserId) && elsewhere.Contains(x.SongId))} comments, "
               + $"{old.Lists.Count(x => persons.Contains(x.UserId) && x.TenantId != tenant.Id)} lists, "
               + $"{old.OwnChords.Count(x => persons.Contains(x.AuthorUserId))} own chords, "
               + $"{old.Visits.Count(x => persons.Contains(x.UserId) && !taken.ContainsKey(x.UserId))} chords opened, "
               + $"{orphans.Count} former personal tenants ({old.Songs.Count(x => orphans.Any(o => o.Id == x.TenantId))} songs)";
    }
}
