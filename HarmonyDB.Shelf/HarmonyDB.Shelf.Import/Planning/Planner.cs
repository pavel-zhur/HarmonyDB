using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Import.Old;
using HarmonyDB.Shelf.Model.Marks;

namespace HarmonyDB.Shelf.Import.Planning;

/// <summary>
/// The mapping of the old shelf to the new one: a function of the old data, the configuration and the resolved
/// addresses. It reads nothing and writes nothing.
/// <list type="bullet">
/// <item>Each configured shared tenant: one private playlist of its people, every song it shows; others' private own
/// chords are left out of it (they are their author's, in the author's own chords).</item>
/// <item>The template set: one public playlist owned by the configured person, with the templates' recommended keys as
/// that person's "here" keys in it.</item>
/// <item>Each person's personal tenants: one private playlist of what they had beyond the templates — every chords of
/// their shown songs that are not chords of the template playlist; a song left with none is not there.</item>
/// <item>Each list: a private playlist of its person; shared for viewing, its tenant's other people are members who do
/// not add; shared for editing, they add too. Its rows are songs: a row of a song brings all its versions, a row of a
/// version that version.</item>
/// <item>Own chords: a song each in their author's own chords playlist.</item>
/// <item>Likes 1–3 outside lists: a heart of the level's colour on the liked chords (a like of a song: on the chords it
/// opened at for the person); one heart a person per chords, the highest.</item>
/// <item>Keys: "mine" is the key a person saw the chords in everywhere (their first like of the version outside lists,
/// else their first in a list); a list row's key is also "here" in the list's playlist.</item>
/// <item>Comments: a note on the song where the song is (several of one person on one song joined); on a template copy,
/// a personal note on the chords.</item>
/// <item>Visits: recent, by the last time.</item>
/// <item>A person's main variant of a song, where the chords it opened at for them are not its first.</item>
/// </list>
/// </summary>
public sealed class Planner(OldShelf old, ImportOptions options, Resolution resolution)
{
    public const string TemplatesKey = "templates";

    public static string TenantKey(int tenantId) => $"tenant:{tenantId}";

    public static string PersonalKey(long person) => $"personal:{person}";

    public static string ListKey(int listId) => $"list:{listId}";

    public static string OwnChordsKey(long person) => $"own:{person}";

    public static string SongKey(int songId) => $"song:{songId}";

    public static string CollectiveKey(Guid collectiveId) => $"collective:{collectiveId:D}";

    private readonly Scope _scope = Scope.Of(old, options);
    private readonly Dictionary<int, OldSong> _songs = old.Songs.ToDictionary(x => x.Id);
    private readonly Dictionary<int, OldTenant> _tenants = old.Tenants.ToDictionary(x => x.Id);
    private readonly ILookup<int, OldVersion> _versions = old.Versions.OrderBy(x => x.Id).ToLookup(x => x.SongId);
    private readonly Dictionary<int, OldVersion> _versionsById = old.Versions.ToDictionary(x => x.Id);
    private readonly ILookup<int, OldLike> _likes = old.Likes.ToLookup(x => x.SongId);
    private readonly List<Unmapped> _unmapped = [];
    private readonly List<Unmapped> _remarks = [];
    private HashSet<string>? _templateIds;

    public Scope Scope => _scope;

    /// <summary>
    /// The addresses the mapping needs resolved: of the versions of the songs taken, and of the people's visits.
    /// </summary>
    public static (IReadOnlyList<string> uris, IReadOnlyList<string> visitIds) Addresses(OldShelf old, ImportOptions options)
    {
        var scope = Scope.Of(old, options);
        var songs = old.Songs.Where(x => scope.IsTakenTenant(x.TenantId) || x.TenantId == options.TemplateTenantId).Select(x => x.Id).Concat(scope.OwnSongsOnly.Values.SelectMany(x => x)).ToHashSet();
        var liked = old.Likes.Where(x => scope.IsTakenFor(x.UserId, x.SongId)).Select(x => x.SongId).ToHashSet();
        var uris = old.Versions
            .Where(x => songs.Contains(x.SongId) || liked.Contains(x.SongId) || x.CollectiveId != null)
            .Select(x => x.Uri)
            .Concat(old.Visits.Where(x => scope.IsTaken(x.UserId) && x.Uri != null).Select(x => x.Uri!))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        var ids = old.Visits.Where(x => scope.IsTaken(x.UserId) && x.Uri == null).Select(x => x.ExternalId!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return (uris, ids);
    }

    public ImportPlan Plan()
    {
        _unmapped.AddRange(_scope.LeftOut);
        if (!_scope.IsTaken(options.TemplateOwnerTelegramId))
            throw new InvalidOperationException("The template playlist's owner is not a person taken.");
        foreach (var tenant in _scope.SharedTenants.Values.Where(x => !_scope.PeopleOf(x.TenantId).Contains(x.OwnerTelegramId)))
            throw new InvalidOperationException($"The owner of the shared tenant {tenant.TenantId} is not one of its people.");

        var playlists = new List<PlaylistPlan> { Templates() };
        playlists.AddRange(_scope.SharedTenants.Values.OrderBy(x => x.TenantId).Select(SharedTenant));
        playlists.AddRange(_scope.People.Keys.Order().Select(Personal).OfType<PlaylistPlan>());
        playlists.AddRange(Lists());
        playlists.AddRange(OwnChords());

        var keys = new List<KeyPlan>();
        keys.AddRange(TemplateKeys());
        keys.AddRange(MineKeys());
        keys.AddRange(ListKeys());
        var mine = keys.Where(x => x.PlaylistKey == null).ToDictionary(x => (x.TelegramId, x.SourceId), x => x.Semitones);
        foreach (var here in keys.Where(x => x.PlaylistKey?.StartsWith("list:") == true && x.Semitones != mine.GetValueOrDefault((x.TelegramId, x.SourceId))))
            _remarks.Add(new("key", here.TelegramId, $"{here.PlaylistKey}, chords {here.SourceId}: here {here.Semitones}, elsewhere {mine.GetValueOrDefault((here.TelegramId, here.SourceId))}", UnmappedReasons.HereDiffersFromMine));

        return new()
        {
            People = People(),
            Playlists = playlists,
            Marks = Marks(),
            Keys = keys,
            Notes = Notes(playlists),
            Recent = Recent(),
            Preferences = Preferences(playlists),
            Unmapped = _unmapped,
            Remarks = _remarks,
        };
    }

    private List<PersonPlan> People()
        => _scope.People.Values.OrderBy(x => x.Id).Select(user =>
        {
            var tenant = _tenants[user.TenantId];
            var permissions = new List<string>();
            if (tenant.ArePdfsAllowed) permissions.Add(ShelfPermissions.Pdf);
            if (user.IllustrationsAllowed) permissions.Add(ShelfPermissions.IllustrationsGenerate);
            permissions.AddRange(tenant.Tags.SelectMany(tag => options.PermissionsByTenantTag.GetValueOrDefault(tag) ?? []));
            permissions.AddRange(options.PermissionsForEveryone);
            return new PersonPlan(user.Id, user.Title, OldRules.Time(user.CreatedOn), permissions.Distinct().ToList());
        }).ToList();

    /// <summary>
    /// The source ids of the template set.
    /// </summary>
    private HashSet<string> TemplateIds
        => _templateIds ??= TemplateVersions().Select(x => resolution.IdOf(x.Uri)).OfType<string>().ToHashSet(StringComparer.Ordinal);

    private IEnumerable<OldVersion> TemplateVersions()
        => old.Versions.Where(x => OldRules.IsTemplate(x, _songs[x.SongId], options.TemplateTenantId));

    private PlaylistPlan Templates()
    {
        var songs = TemplateVersions().GroupBy(x => x.SongId).Select(x => _songs[x.Key]).OrderBy(x => x.Index).ThenBy(x => x.Id);
        return new()
        {
            Key = TemplatesKey,
            Title = options.TemplatePlaylistTitle,
            Access = PlaylistAccess.Public,
            AddPolicy = PlaylistAddPolicy.Admins,
            SystemKind = PlaylistSystemKind.None,
            OwnerTelegramId = options.TemplateOwnerTelegramId,
            Members = [],
            Songs = Songs(TemplatesKey, songs.Select(s => (s, (IReadOnlyList<OldVersion>)_versions[s.Id].Where(v => OldRules.IsTemplate(v, s, options.TemplateTenantId)).ToList())), null),
        };
    }

    private IEnumerable<KeyPlan> TemplateKeys()
        => TemplateVersions()
            .Select(x => (id: resolution.IdOf(x.Uri), semitones: OldRules.PublishedTransposition(x.PublishedSettings), at: OldRules.Time(x.CreatedOn)))
            .Where(x => x.id != null && x.semitones is not (null or 0))
            .DistinctBy(x => x.id)
            .Select(x => new KeyPlan(options.TemplateOwnerTelegramId, x.id!, TemplatesKey, x.semitones!.Value, x.at));

    private PlaylistPlan SharedTenant(SharedTenantOptions tenant)
    {
        var key = TenantKey(tenant.TenantId);
        var songs = old.Songs
            .Where(x => x.TenantId == tenant.TenantId && OldRules.IsShown(x))
            .OrderBy(x => x.Index).ThenBy(x => x.Id)
            .Select(s => (s, (IReadOnlyList<OldVersion>)_versions[s.Id].Where(v => v.CollectiveType is null or OldCollectiveType.Public).ToList()));

        return new()
        {
            Key = key,
            Title = tenant.Title,
            Access = PlaylistAccess.Private,
            AddPolicy = PlaylistAddPolicy.AllMembers,
            SystemKind = PlaylistSystemKind.None,
            OwnerTelegramId = tenant.OwnerTelegramId,
            Members = _scope.PeopleOf(tenant.TenantId).Where(x => x != tenant.OwnerTelegramId).Select(x => new MemberPlan(x, PlaylistRole.Member)).ToList(),
            Songs = Songs(key, songs, null),
        };
    }

    private PlaylistPlan? Personal(long person)
    {
        var key = PersonalKey(person);
        var tenants = _scope.PersonalTenants.Where(x => x.Value == person).Select(x => x.Key).ToHashSet();
        var own = _scope.OwnSongsOnly.GetValueOrDefault(person);
        if (tenants.Count == 0 && own == null)
            return null;

        var songs = old.Songs
            .Where(x => (tenants.Contains(x.TenantId) || own?.Contains(x.Id) == true) && OldRules.IsShown(x))
            .OrderBy(x => OldRules.IsTemplateCopy(x, _tenants[x.TenantId]) ? 0 : 1).ThenBy(x => x.TenantId).ThenBy(x => x.Index).ThenBy(x => x.Id)
            .Select(s => (s, (IReadOnlyList<OldVersion>)_versions[s.Id].Where(v => OldRules.IsVisible(v, person)).ToList()));
        var planned = Songs(key, songs, TemplateIds);
        return planned.Count == 0
            ? null
            : new()
            {
                Key = key,
                Title = options.PersonalPlaylistTitle,
                Access = PlaylistAccess.Private,
                AddPolicy = PlaylistAddPolicy.Admins,
                SystemKind = PlaylistSystemKind.None,
                OwnerTelegramId = person,
                Members = [],
                Songs = planned,
            };
    }

    private IEnumerable<PlaylistPlan> Lists()
    {
        foreach (var list in old.Lists.OrderBy(x => x.Id))
        {
            if (!Planned(list))
            {
                _unmapped.Add(new("list", null, $"list {list.Id} of user {list.UserId}", UnmappedReasons.ListOwnerNotTaken));
                continue;
            }

            var key = ListKey(list.Id);
            var shared = list.Access != OldListAccess.Private && _scope.SharedTenants.ContainsKey(list.TenantId);
            var members = shared
                ? _scope.PeopleOf(list.TenantId).Where(x => x != list.UserId).Select(x => new MemberPlan(x, PlaylistRole.Member)).ToList()
                : [];
            var rows = old.Likes.Where(x => x.ListId == list.Id).ToList();
            var songs = new List<(OldSong, IReadOnlyList<OldVersion>, DateTime, long)>();
            foreach (var row in rows.GroupBy(x => x.SongId).OrderBy(x => x.Min(r => r.CreatedOn)).ThenBy(x => x.Key))
            {
                var song = _songs[row.Key];
                if (!OldRules.IsShown(song))
                {
                    _unmapped.Add(new("list row", list.UserId, $"list {list.Id}, song {song.Id}", UnmappedReasons.SongNotShown));
                    continue;
                }

                var visible = _versions[song.Id].Where(v => v.CollectiveType is null or OldCollectiveType.Public || (members.Count == 0 && v.UserId == list.UserId)).ToList();
                var versions = row.Any(x => x.VersionId == null)
                    ? visible
                    : visible.Where(v => row.Any(r => r.VersionId == v.Id)).ToList();
                var first = row.OrderBy(x => x.CreatedOn).ThenBy(x => x.Id).First();
                songs.Add((song, versions, first.CreatedOn, first.UserId));
            }

            yield return new()
            {
                Key = key,
                Title = list.Name,
                Access = PlaylistAccess.Private,
                AddPolicy = shared && list.Access is OldListAccess.SharedEditMulti or OldListAccess.SharedEditSingle ? PlaylistAddPolicy.AllMembers : PlaylistAddPolicy.Admins,
                SystemKind = PlaylistSystemKind.None,
                OwnerTelegramId = list.UserId,
                Members = members,
                Songs = Songs(key, songs.Select(x => (x.Item1, x.Item2)), null, songs.ToDictionary(x => x.Item1.Id, x => (x.Item3, x.Item4))),
            };
        }
    }

    private IEnumerable<KeyPlan> ListKeys()
        => old.Likes
            .Where(x => x.ListId != null && x.VersionId != null && x.Transpose is not (null or 0) && _scope.IsTakenFor(x.UserId, x.SongId) && Planned(old.Lists.Single(l => l.Id == x.ListId)) && OldRules.IsShown(_songs[x.SongId]))
            .Select(x => (like: x, id: resolution.IdOf(_versionsById[x.VersionId!.Value].Uri)))
            .Where(x => x.id != null)
            .GroupBy(x => (x.like.UserId, x.id, x.like.ListId))
            .Select(x => x.OrderBy(l => l.like.Id).First())
            .Select(x => new KeyPlan(x.like.UserId, x.id!, ListKey(x.like.ListId!.Value), x.like.Transpose!.Value, OldRules.Time(x.like.CreatedOn)));

    private IEnumerable<PlaylistPlan> OwnChords()
    {
        foreach (var author in old.OwnChords.GroupBy(x => x.AuthorUserId).OrderBy(x => x.Key))
        {
            if (!_scope.IsTaken(author.Key))
            {
                _unmapped.AddRange(author.Select(x => new Unmapped("own chords", null, $"own chords {x.Id} of user {author.Key}", UnmappedReasons.ListOwnerNotTaken)));
                continue;
            }

            var key = OwnChordsKey(author.Key);
            var songs = new List<SongPlan>();
            var i = 0;
            foreach (var chords in author.OrderBy(x => x.CreatedOn).ThenBy(x => x.Id))
            {
                if (chords.IsDeleted)
                {
                    _unmapped.Add(new("own chords", author.Key, $"own chords {chords.Id}", UnmappedReasons.OwnChordsDeleted));
                    continue;
                }

                var version = old.Versions.Where(x => x.CollectiveId == chords.Id).OrderBy(x => x.Id).FirstOrDefault();
                if (version == null)
                {
                    _unmapped.Add(new("own chords", author.Key, $"own chords {chords.Id}", UnmappedReasons.OwnChordsWithoutAddress));
                    continue;
                }

                if (resolution.IdOf(version.Uri) is not { } id)
                {
                    _unmapped.Add(new("own chords", author.Key, $"own chords {chords.Id} at {version.Uri}", UnmappedReasons.AddressNotResolved, true));
                    continue;
                }

                var at = OldRules.Time(chords.CreatedOn);
                songs.Add(new(CollectiveKey(chords.Id), chords.Title, chords.Authors, ++i, author.Key, at, [new(id, author.Key, at)]));
            }

            yield return new()
            {
                Key = key,
                Title = null,
                Access = PlaylistAccess.Private,
                AddPolicy = PlaylistAddPolicy.Admins,
                SystemKind = PlaylistSystemKind.OwnChords,
                OwnerTelegramId = author.Key,
                Members = [],
                Songs = songs,
            };
        }
    }

    /// <summary>
    /// The songs of a playlist from old songs with the versions they bring, in order; chords already in an earlier song
    /// of the playlist, chords of <paramref name="excluded"/> and unresolved ones are not variants; a song left with no
    /// variant is not there.
    /// </summary>
    /// <param name="rows">For a list: the song's first row (when, by whom), which is when and by whom it was added.</param>
    private List<SongPlan> Songs(string playlistKey, IEnumerable<(OldSong song, IReadOnlyList<OldVersion> versions)> songs, IReadOnlySet<string>? excluded, IReadOnlyDictionary<int, (DateTime at, long by)>? rows = null)
    {
        var taken = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new List<SongPlan>();
        foreach (var (song, versions) in songs)
        {
            if (!_versions[song.Id].Any())
            {
                _unmapped.Add(new("song", null, $"{playlistKey}, song {song.Id} \"{song.Title}\"", UnmappedReasons.SongWithoutVersions));
                continue;
            }

            var items = new List<ItemPlan>();
            foreach (var version in versions.OrderBy(x => x.Id))
            {
                var id = resolution.IdOf(version.Uri);
                if (id == null)
                {
                    _unmapped.Add(new("variant", null, $"{playlistKey}, song {song.Id}, version {version.Id} at {version.Uri}", UnmappedReasons.AddressNotResolved, true));
                    continue;
                }

                if (excluded != null && excluded.Contains(id))
                    continue;

                if (taken.TryGetValue(id, out var other))
                {
                    if (other != song.Id)
                        _unmapped.Add(new("variant", null, $"{playlistKey}, song {song.Id}, version {version.Id} (as in song {other})", UnmappedReasons.DuplicateInPlaylist));
                    continue;
                }

                taken[id] = song.Id;
                items.Add(new(id, Person(rows?.GetValueOrDefault(song.Id).by ?? version.UserId), OldRules.Time(rows?.GetValueOrDefault(song.Id).at ?? version.CreatedOn)));
            }

            if (items.Count == 0)
                continue;

            if (OldRules.LooksMisdecoded(song.Title) || song.Artists.Any(OldRules.LooksMisdecoded))
                _remarks.Add(new("song", null, $"{playlistKey}, song {song.Id}: \"{song.Title}\" by \"{string.Join(", ", song.Artists)}\"", UnmappedReasons.Misdecoded));

            var row = rows?.GetValueOrDefault(song.Id);
            result.Add(new(
                SongKey(song.Id),
                song.Title,
                song.Artists,
                result.Count + 1,
                Person(row?.by ?? song.CreatedByUserId),
                OldRules.Time(row?.at ?? song.CreatedOn),
                items));
        }

        return result;
    }

    private long? Person(long userId) => _scope.IsTaken(userId) ? userId : null;

    /// <summary>
    /// Whether a list becomes a playlist: its person is taken, with all their things.
    /// </summary>
    private bool Planned(OldList list) => _scope.IsTaken(list.UserId) && !_scope.OwnSongsOnly.ContainsKey(list.UserId);

    private List<MarkPlan> Marks()
    {
        var hearts = new Dictionary<(long person, string id), OldLike>();
        foreach (var like in old.Likes.Where(x => x.ListId == null && _scope.IsTakenFor(x.UserId, x.SongId)).OrderBy(x => x.Id))
        {
            var song = _songs[like.SongId];
            if (like.Level == 0 && like.VersionId == null)
            {
                _unmapped.Add(new("like", like.UserId, $"like {like.Id} of song {song.Id}", UnmappedReasons.NoVersionNoLevel));
                continue;
            }

            if (!MarkKinds.OldLikeLevels.ContainsKey(like.Level))
            {
                _unmapped.Add(new("like", like.UserId, $"like {like.Id} (level {like.Level}) of song {song.Id}", UnmappedReasons.UnknownLevel));
                continue;
            }

            if (!OldRules.IsShown(song))
            {
                _unmapped.Add(new("like", like.UserId, $"like {like.Id} of song {song.Id}", UnmappedReasons.SongNotShown));
                continue;
            }

            if (like.VersionId == null && like.Transpose is not (null or 0))
                _unmapped.Add(new("transposition", like.UserId, $"like {like.Id} of song {song.Id}: {like.Transpose}", UnmappedReasons.TranspositionWithoutVersion));

            var version = like.VersionId is { } versionId
                ? _versionsById[versionId]
                : OldRules.DefaultVersion(like.UserId, _versions[song.Id].Where(x => OldRules.IsVisible(x, like.UserId)).ToList(), _likes[song.Id].ToList());
            if (version == null)
            {
                _unmapped.Add(new("like", like.UserId, $"like {like.Id} of song {song.Id}", UnmappedReasons.SongWithoutVersions));
                continue;
            }

            if (resolution.IdOf(version.Uri) is not { } id)
            {
                _unmapped.Add(new("like", like.UserId, $"like {like.Id} of song {song.Id} at {version.Uri}", UnmappedReasons.AddressNotResolved, true));
                continue;
            }

            if (hearts.TryGetValue((like.UserId, id), out var kept))
            {
                _unmapped.Add(new("like", like.UserId, $"like {like.Id} (level {like.Level}) of song {song.Id}, beside level {kept.Level}", UnmappedReasons.MergedHeart));
                if (kept.Level >= like.Level)
                    continue;
            }

            hearts[(like.UserId, id)] = like;
        }

        return hearts.OrderBy(x => x.Key.person).ThenBy(x => x.Key.id, StringComparer.Ordinal)
            .Select(x => new MarkPlan(x.Key.person, x.Key.id, MarkKinds.OldLikeLevels[x.Value.Level].Key, OldRules.Time(x.Value.CreatedOn))).ToList();
    }

    private IEnumerable<KeyPlan> MineKeys()
        => old.Likes
            .Where(x => x.VersionId != null && _scope.IsTakenFor(x.UserId, x.SongId) && OldRules.IsShown(_songs[x.SongId]))
            .Select(x => (like: x, id: resolution.IdOf(_versionsById[x.VersionId!.Value].Uri)))
            .Where(x => x.id != null)
            .GroupBy(x => (x.like.UserId, x.id))
            .Select(x => (x.Key, like: OldRules.DefaultTranspositionLike(x.Select(l => l.like))))
            .Where(x => x.like?.Transpose is not (null or 0))
            .OrderBy(x => x.Key.UserId).ThenBy(x => x.Key.id, StringComparer.Ordinal)
            .Select(x => new KeyPlan(x.Key.UserId, x.Key.id!, null, x.like!.Transpose!.Value, OldRules.Time(x.like.CreatedOn)));

    private List<NotePlan> Notes(IReadOnlyList<PlaylistPlan> playlists)
    {
        var byKey = playlists.ToDictionary(x => x.Key);
        var notes = new List<(long person, string? playlist, string? song, string? chords, OldComment comment, string? on)>();
        foreach (var comment in old.Comments.Where(x => _scope.IsTakenFor(x.UserId, x.SongId)).OrderBy(x => x.CreatedOn).ThenBy(x => x.Id))
        {
            var song = _songs[comment.SongId];
            if (!OldRules.IsShown(song))
            {
                _unmapped.Add(new("comment", comment.UserId, $"comment {comment.Id} of song {song.Id}", UnmappedReasons.SongNotShown));
                continue;
            }

            var playlistKey = _scope.SharedTenants.ContainsKey(song.TenantId) ? TenantKey(song.TenantId)
                : _scope.PersonalTenants.TryGetValue(song.TenantId, out var owner) ? PersonalKey(owner)
                : _scope.OwnSongsOnly.ContainsKey(comment.UserId) ? PersonalKey(comment.UserId)
                : null;
            if (playlistKey == null)
            {
                _unmapped.Add(new("comment", comment.UserId, $"comment {comment.Id} of song {song.Id}", UnmappedReasons.TenantNotConfigured));
                continue;
            }

            var version = comment.VersionId is { } versionId
                ? _versionsById[versionId]
                : OldRules.DefaultVersion(comment.UserId, _versions[song.Id].Where(x => OldRules.IsVisible(x, comment.UserId)).ToList(), _likes[song.Id].ToList());
            var id = version == null ? null : resolution.IdOf(version.Uri);
            var planned = byKey.GetValueOrDefault(playlistKey)?.Songs;
            var target = planned?.FirstOrDefault(x => id != null && x.Items.Any(i => i.SourceId == id))
                         ?? (comment.VersionId == null ? planned?.FirstOrDefault(x => x.Key == SongKey(song.Id)) : null);
            if (target != null)
                notes.Add((comment.UserId, playlistKey, target.Key, null, comment, comment.VersionId != null ? id : null));
            else if (id != null)
                notes.Add((comment.UserId, null, null, id, comment, null));
            else
                _unmapped.Add(new("comment", comment.UserId, $"comment {comment.Id} of song {song.Id}", version == null ? UnmappedReasons.SongWithoutVersions : UnmappedReasons.AddressNotResolved, version != null));
        }

        return notes
            .GroupBy(x => (x.person, x.playlist, x.song, x.chords))
            .Select(x => new NotePlan(x.Key.person, x.Key.playlist, x.Key.song, x.Key.chords, x.Select(c => new NotePart(c.comment.Text, c.on, OldRules.Time(c.comment.CreatedOn))).ToList()))
            .ToList();
    }

    private List<RecentPlan> Recent()
    {
        var recent = new Dictionary<(long person, string id), DateTime>();
        foreach (var visit in old.Visits.Where(x => _scope.IsTaken(x.UserId)))
        {
            var id = visit.Uri != null ? resolution.IdOf(visit.Uri) : resolution.ConfirmedId(visit.ExternalId!);
            if (id == null)
            {
                _unmapped.Add(visit.Uri != null
                    ? new("visit", visit.UserId, visit.Uri, UnmappedReasons.AddressNotResolved, true)
                    : new("visit", visit.UserId, visit.ExternalId!, UnmappedReasons.IdNotOpen));
                continue;
            }

            if (!recent.TryGetValue((visit.UserId, id), out var at) || at < visit.LastViewedOn)
                recent[(visit.UserId, id)] = visit.LastViewedOn;
        }

        return recent
            .Where(x => OldRules.Time(x.Value) != null)
            .OrderBy(x => x.Key.person).ThenBy(x => x.Value)
            .Select(x => new RecentPlan(x.Key.person, x.Key.id, OldRules.Time(x.Value)!.Value))
            .ToList();
    }

    private List<PreferencePlan> Preferences(IReadOnlyList<PlaylistPlan> playlists)
    {
        var preferences = new List<PreferencePlan>();
        foreach (var playlist in playlists.Where(x => x.Key != TemplatesKey && x.SystemKind == PlaylistSystemKind.None))
        {
            var people = playlist.Members.Select(x => x.TelegramId).Prepend(playlist.OwnerTelegramId).ToList();
            foreach (var song in playlist.Songs.Where(x => x.Items.Count > 1))
            {
                var oldSong = _songs[int.Parse(song.Key["song:".Length..])];
                foreach (var person in people)
                {
                    var main = OldRules.DefaultVersion(person, _versions[oldSong.Id].Where(x => OldRules.IsVisible(x, person)).ToList(), _likes[oldSong.Id].ToList());
                    var id = main == null ? null : resolution.IdOf(main.Uri);
                    if (id != null && song.Items.Any(x => x.SourceId == id))
                        preferences.Add(new(person, playlist.Key, song.Key, id));
                }
            }
        }

        return preferences;
    }
}

/// <summary>
/// The built-in permissions of the shelf that carry over what the old shelf gated (the same names as the app's).
/// </summary>
public static class ShelfPermissions
{
    public const string Pdf = "pdf";
    public const string IllustrationsGenerate = "illustrations.generate";
}
