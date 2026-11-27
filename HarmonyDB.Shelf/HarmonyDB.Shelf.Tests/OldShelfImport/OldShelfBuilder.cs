using HarmonyDB.Shelf.Import;
using HarmonyDB.Shelf.Import.Old;
using HarmonyDB.Shelf.Import.Planning;

namespace HarmonyDB.Shelf.Tests.OldShelfImport;

/// <summary>
/// Made-up old shelf data for the import's tests: a shared tenant 1 (which holds the templates), people, songs with
/// versions at example addresses, every address resolving to "id:" + its last segment unless said otherwise.
/// </summary>
internal sealed class OldShelfBuilder
{
    public const int SharedTenant = 1;
    public const long Keeper = 100;
    public const long Alice = 101;

    private readonly List<OldTenant> _tenants = [new(SharedTenant, "Shared", ["T"], true, 2000)];
    private readonly List<OldUser> _users = [new(Keeper, SharedTenant, "Keeper", true, DateTime.MinValue), new(Alice, SharedTenant, "Alice", false, DateTime.MinValue)];
    private readonly List<OldSong> _songs = [];
    private readonly List<OldVersion> _versions = [];
    private readonly List<OldLike> _likes = [];
    private readonly List<OldList> _lists = [];
    private readonly List<OldComment> _comments = [];
    private readonly List<OldVisit> _visits = [];
    private readonly List<OldOwnChords> _ownChords = [];
    private readonly HashSet<string> _unresolved = [];
    private int _nextId = 1;

    public static string Uri(string name) => $"https://chords.example/{name}";

    public static string Id(string name) => $"id:{name}";

    public OldShelfBuilder Tenant(int id, int latestUsedIndex = 0, string? description = null, bool pdfs = false, params string[] tags)
    {
        _tenants.Add(new(id, description, tags, pdfs, latestUsedIndex));
        return this;
    }

    public OldShelfBuilder User(long id, int tenant, string title = "Someone", bool illustrations = false)
    {
        _users.Add(new(id, tenant, title, illustrations, new(2024, 1, 1)));
        return this;
    }

    /// <summary>
    /// A song with versions at the named addresses; returns its id.
    /// </summary>
    public int Song(int tenant, int index, string title, params string[] versions) => Song(tenant, index, title, OldSongStatus.Live, Keeper, versions);

    public int Song(int tenant, int index, string title, string status, long by, params string[] versions)
    {
        var id = _nextId++;
        _songs.Add(new(id, tenant, index, title, ["artist"], status, by, new DateTime(2024, 1, 1).AddDays(id)));
        foreach (var name in versions)
            Version(id, name, by);
        return id;
    }

    public int Version(int song, string name, long by, bool template = false, string? published = null, string? collectiveType = null, Guid? collectiveId = null)
    {
        var id = _nextId++;
        _versions.Add(new(id, song, Uri(name), by, new DateTime(2024, 1, 1).AddMinutes(id), published, collectiveType, collectiveId, template));
        return id;
    }

    /// <summary>
    /// A template song of the shared tenant: its versions are default templates.
    /// </summary>
    public int Template(int index, string title, string name, string? published = null)
    {
        var song = Song(SharedTenant, index, title);
        Version(song, name, Keeper, template: true, published: published);
        return song;
    }

    public int VersionOf(int song, string name) => _versions.Single(x => x.SongId == song && x.Uri == Uri(name)).Id;

    public OldShelfBuilder Like(long user, int song, int? version, int level, int? transpose = null, int? list = null)
    {
        _likes.Add(new(_nextId++, user, song, version, level, transpose, new DateTime(2024, 2, 1).AddMinutes(_nextId), list));
        return this;
    }

    public int List(long user, int tenant, string name, string access)
    {
        var id = _nextId++;
        _lists.Add(new(id, tenant, user, name, 0, access));
        return id;
    }

    public OldShelfBuilder Comment(long user, int song, int? version, string text)
    {
        _comments.Add(new(_nextId++, user, song, version, text, new DateTime(2024, 3, 1).AddMinutes(_nextId)));
        return this;
    }

    public OldShelfBuilder Visit(long user, string name)
    {
        _visits.Add(new(user, Uri(name), null, new DateTime(2024, 4, 1), $"Title of {name}", ["An artist"], "A source"));
        return this;
    }

    /// <summary>
    /// A visit kept by the id the person opened (a search's result), not by an address.
    /// </summary>
    public OldShelfBuilder VisitById(long user, string id, string title)
    {
        _visits.Add(new(user, null, id, new DateTime(2024, 4, 2), title, ["Someone"], "Local"));
        return this;
    }

    public OldShelfBuilder OwnChords(Guid id, long author, string title)
    {
        _ownChords.Add(new(id, author, false, title, ["author"], new DateTime(2024, 5, 1)));
        return this;
    }

    public OldShelfBuilder Unresolved(string name)
    {
        _unresolved.Add(Uri(name));
        return this;
    }

    public OldShelf Build() => new()
    {
        Tenants = _tenants,
        Users = _users,
        Songs = _songs,
        Versions = _versions,
        Likes = _likes,
        Lists = _lists,
        Comments = _comments,
        Visits = _visits,
        OwnChords = _ownChords,
    };

    public Resolution Resolution()
        => new(_versions.Select(x => x.Uri).Concat(_visits.Where(x => x.Uri != null).Select(x => x.Uri!)).Distinct().ToDictionary(
                x => x,
                x => _unresolved.Contains(x) ? new ResolvedAddress(null, "notResolved") : new ResolvedAddress($"id:{x.Split('/')[^1]}", "resolved")));

    public static ImportOptions Options(params SharedTenantOptions[] extra) => new()
    {
        TemplateTenantId = SharedTenant,
        TemplateOwnerTelegramId = Keeper,
        TemplatePlaylistTitle = "Templates",
        PersonalPlaylistTitle = "Mine",
        SharedTenants = [new() { TenantId = SharedTenant, Title = "Club", OwnerTelegramId = Keeper }, .. extra],
        PermissionsByTenantTag = new() { ["T"] = ["custom-key"] },
    };

    public ImportPlan Plan(ImportOptions? options = null) => new Planner(Build(), options ?? Options(), Resolution()).Plan();
}
