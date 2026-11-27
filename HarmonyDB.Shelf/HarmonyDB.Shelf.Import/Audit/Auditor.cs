using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Import.Old;
using HarmonyDB.Shelf.Import.Planning;
using HarmonyDB.Shelf.Import.Writing;
using HarmonyDB.Shelf.Model.Marks;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Import.Audit;

/// <summary>
/// Compares, person by person, what each had in the old shelf with what they have in the shelf, reading both and writing
/// nothing. It counts from the old rows themselves (with the old shelf's own rules and the addresses as resolved), not
/// from the import's plan: songs and their chords in the shared libraries, the personal playlist and the lists (with
/// their titles and members), the order of the variants, hearts by level, "mine" and "here" keys, comments, own chords,
/// recent, permissions; and the template playlist. What waits for its address is counted apart, not as a failure.
/// </summary>
public sealed class Auditor(OldShelf old, ImportOptions options, ShelfDbContext db)
{
    private int _failures;
    private TextWriter _output = TextWriter.Null;

    /// <returns>The number of failures.</returns>
    public async Task<int> RunAsync(TextWriter output, CancellationToken cancellationToken)
    {
        _output = output;
        var scope = Scope.Of(old, options);
        var ledger = await Ledger.ReadAsync(db, cancellationToken);
        // what the last run had: the addresses resolved since (by the second pass) are counted as waiting
        var lastRun = ledger.Get(Ledger.LastRunKey)?.Value is { } value ? DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : (DateTimeOffset?)null;
        var addresses = await Ledger.ReadAddressesAsync(db, cancellationToken, lastRun);
        string? Id(string uri) => addresses.GetValueOrDefault(("uri", uri))?.SourceId;

        var songs = old.Songs.ToDictionary(x => x.Id);
        var tenants = old.Tenants.ToDictionary(x => x.Id);
        var versions = old.Versions.ToLookup(x => x.SongId);
        var versionsById = old.Versions.ToDictionary(x => x.Id);
        var likes = old.Likes.ToLookup(x => x.SongId);

        var subjects = scope.People.Keys.Select(x => x.ToString()).ToList();
        var accounts = await db.AccountLogins.Where(x => x.Provider == ShelfWriter.TelegramProvider && subjects.Contains(x.Subject))
            .ToDictionaryAsync(x => long.Parse(x.Subject), x => x.AccountId, cancellationToken);
        var accountIds = accounts.Values.ToList();
        var marks = (await db.Marks.Where(x => accountIds.Contains(x.AccountId)).Select(x => new { x.AccountId, x.Chords.SourceId, x.Kind }).ToListAsync(cancellationToken)).ToLookup(x => x.AccountId);
        var keys = (await db.Transpositions.Where(x => accountIds.Contains(x.AccountId)).Select(x => new { x.AccountId, x.Chords.SourceId, x.PlaylistId, x.Semitones }).ToListAsync(cancellationToken)).ToLookup(x => x.AccountId);
        var notes = (await db.Notes.Where(x => accountIds.Contains(x.AccountId)).Select(x => new { x.AccountId, x.Text }).ToListAsync(cancellationToken)).ToLookup(x => x.AccountId, x => x.Text);
        var recent = (await db.Recent.Where(x => accountIds.Contains(x.AccountId)).Select(x => new { x.AccountId, x.Chords!.SourceId }).ToListAsync(cancellationToken)).ToLookup(x => x.AccountId, x => x.SourceId);
        var permissions = (await db.AccountPermissions.Where(x => accountIds.Contains(x.AccountId)).ToListAsync(cancellationToken)).ToLookup(x => x.AccountId, x => x.Permission);
        var levels = MarkKinds.OldLikeLevels.ToDictionary(x => x.Value.Key, x => x.Key);

        async Task<PlaylistState?> PlaylistAsync(string key)
        {
            if (ledger.Get($"playlist:{key}") is not { Id: { } id })
                return null;
            var playlist = await db.Playlists.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (playlist == null)
                return null;
            var members = await db.PlaylistMembers.Where(x => x.PlaylistId == id).ToDictionaryAsync(x => x.AccountId, x => x.Role, cancellationToken);
            var items = await db.PlaylistSongItems.Where(x => x.Song.PlaylistId == id && x.Song.DeletedAt == null && x.ChordsId != null)
                .OrderBy(x => x.Position).ThenBy(x => x.Id)
                .Select(x => new { x.SongId, x.Chords!.SourceId }).ToListAsync(cancellationToken);
            var order = await db.PlaylistSongs.Where(x => x.PlaylistId == id).OrderBy(x => x.Position).ThenBy(x => x.Id).Select(x => x.Id).ToListAsync(cancellationToken);
            return new(playlist, members, items.ToLookup(x => x.SongId, x => x.SourceId), items.Select(x => x.SourceId).ToHashSet(StringComparer.Ordinal), order);
        }

        var templateIds = old.Versions.Where(x => OldRules.IsTemplate(x, songs[x.SongId], options.TemplateTenantId)).Select(x => Id(x.Uri)).OfType<string>().ToHashSet(StringComparer.Ordinal);

        // the template playlist
        output.WriteLine("Template playlist:");
        {
            var playlist = await PlaylistAsync(Planner.TemplatesKey);
            var had = old.Versions.Where(x => OldRules.IsTemplate(x, songs[x.SongId], options.TemplateTenantId)).ToList();
            Check(playlist != null, "  it exists");
            if (playlist != null)
            {
                Check(playlist.Playlist.Access == PlaylistAccess.Public, "  it is public");
                Check(accounts.TryGetValue(options.TemplateOwnerTelegramId, out var owner) && playlist.Playlist.OwnerId == owner && playlist.Members.GetValueOrDefault(owner) == PlaylistRole.Owner, "  its owner is the configured person, in both places");
                Compare("  template chords", had.Select(x => x.Uri), Id, playlist.Ids);
                var hadKeys = had.Select(x => (id: Id(x.Uri), t: OldRules.PublishedTransposition(x.PublishedSettings))).Where(x => x.id != null && x.t is not (null or 0)).Select(x => $"{x.id}:{x.t}").ToHashSet();
                var hasKeys = keys[owner].Where(x => x.PlaylistId == playlist.Playlist.Id).Select(x => $"{x.SourceId}:{x.Semitones}").ToHashSet();
                Check(hadKeys.SetEquals(hasKeys), $"  recommended keys as the owner's \"here\": had {hadKeys.Count}, has {hasKeys.Count}");
            }
        }

        output.WriteLine();
        output.WriteLine("People (had → has; waiting: the address is not resolved yet):");
        foreach (var person in scope.People.Values.OrderBy(x => x.TenantId).ThenBy(x => x.Id))
        {
            var p = person.Id;
            var line = new List<string>();
            if (!accounts.TryGetValue(p, out var account))
            {
                Fail($"{p} {person.Title}: no account");
                continue;
            }

            // permissions
            var tenant = tenants[person.TenantId];
            var hadPermissions = new List<string>();
            if (tenant.ArePdfsAllowed) hadPermissions.Add(ShelfPermissions.Pdf);
            if (person.IllustrationsAllowed) hadPermissions.Add(ShelfPermissions.IllustrationsGenerate);
            hadPermissions.AddRange(tenant.Tags.SelectMany(t => options.PermissionsByTenantTag.GetValueOrDefault(t) ?? []));
            hadPermissions.AddRange(options.PermissionsForEveryone);
            var missingPermissions = hadPermissions.Except(permissions[account]).ToList();
            line.Add($"permissions {hadPermissions.Count - missingPermissions.Count}/{hadPermissions.Count}");
            if (missingPermissions.Count > 0) Fail($"{p}: permissions missing: {missingPermissions.Count}");

            // hearts, by level
            var hadHearts = new Dictionary<string, int>(StringComparer.Ordinal);
            var waitingHearts = 0;
            foreach (var like in old.Likes.Where(x => x.UserId == p && x.ListId == null && (x.Level > 0 || x.VersionId != null) && MarkKinds.OldLikeLevels.ContainsKey(x.Level) && scope.IsTakenFor(p, x.SongId) && OldRules.IsShown(songs[x.SongId])))
            {
                var version = like.VersionId is { } v ? versionsById[v] : OldRules.DefaultVersion(p, versions[like.SongId].Where(x => OldRules.IsVisible(x, p)).ToList(), likes[like.SongId].ToList());
                if (version == null) continue;
                if (Id(version.Uri) is not { } id) { waitingHearts++; continue; }
                hadHearts[id] = Math.Max(hadHearts.GetValueOrDefault(id), like.Level);
            }

            var hasHearts = marks[account].Where(x => levels.ContainsKey(x.Kind)).GroupBy(x => x.SourceId).ToDictionary(x => x.Key, x => x.Max(m => levels[m.Kind]), StringComparer.Ordinal);
            var heartsOk = hadHearts.Count(x => hasHearts.GetValueOrDefault(x.Key) == x.Value);
            line.Add($"hearts {heartsOk}/{hadHearts.Count} [{string.Join(" ", Enumerable.Range(0, 4).Reverse().Select(l => $"{l}:{hadHearts.Count(x => x.Value == l)}→{hasHearts.Count(x => x.Value == l)}"))}]{Waiting(waitingHearts)}");
            if (heartsOk != hadHearts.Count || hasHearts.Count != hadHearts.Count)
                Fail($"{p}: hearts {heartsOk}/{hadHearts.Count}, has {hasHearts.Count}; had, not had: {string.Join(", ", hadHearts.Where(x => hasHearts.GetValueOrDefault(x.Key, -1) != x.Value).Take(10).Select(x => $"{x.Key}:{x.Value}"))}; has, not had: {string.Join(", ", hasHearts.Where(x => hadHearts.GetValueOrDefault(x.Key, -1) != x.Value).Take(10).Select(x => $"{x.Key}:{x.Value}"))}");

            // "mine" keys
            var hadMine = old.Likes.Where(x => x.UserId == p && x.VersionId != null && scope.IsTakenFor(p, x.SongId) && OldRules.IsShown(songs[x.SongId]))
                .Select(x => (like: x, id: Id(versionsById[x.VersionId!.Value].Uri))).Where(x => x.id != null)
                .GroupBy(x => x.id!).Select(x => (id: x.Key, t: OldRules.DefaultTransposition(x.Select(l => l.like)))).Where(x => x.t is not (null or 0))
                .Select(x => $"{x.id}:{x.t}").ToHashSet();
            var hasMine = keys[account].Where(x => x.PlaylistId == null).Select(x => $"{x.SourceId}:{x.Semitones}").ToHashSet();
            line.Add($"mine {hadMine.Intersect(hasMine).Count()}/{hadMine.Count}");
            if (!hadMine.SetEquals(hasMine)) Fail($"{p}: mine keys had {hadMine.Count}, has {hasMine.Count}, in common {hadMine.Intersect(hasMine).Count()}");

            // the shared library
            if (scope.SharedTenants.ContainsKey(person.TenantId))
            {
                var playlist = await PlaylistAsync(Planner.TenantKey(person.TenantId));
                if (playlist == null) { Fail($"{p}: the shared library's playlist is not there"); }
                else
                {
                    var expectedRole = scope.SharedTenants[person.TenantId].OwnerTelegramId == p ? PlaylistRole.Owner : PlaylistRole.Member;
                    if (playlist.Members.GetValueOrDefault(account, (PlaylistRole)(-1)) != expectedRole) Fail($"{p}: not a {expectedRole} of the shared library");
                    var shown = old.Songs.Where(x => x.TenantId == person.TenantId && OldRules.IsShown(x)).ToList();
                    var uris = shown.SelectMany(x => versions[x.Id]).Where(x => x.CollectiveType is null or OldCollectiveType.Public).Select(x => x.Uri);
                    var oldSongOf = shown.Select(x => (id: ledger.Get($"song:{Planner.TenantKey(person.TenantId)}:{Planner.SongKey(x.Id)}")?.Id, song: x)).Where(x => x.id != null).ToDictionary(x => x.id!.Value, x => x.song);
                    var indexes = playlist.Order.Where(oldSongOf.ContainsKey).Select(x => (oldSongOf[x].Index, oldSongOf[x].Id)).ToList();
                    var songOrderOff = indexes.Zip(indexes.Skip(1)).Count(x => x.First.CompareTo(x.Second) > 0);
                    line.Add($"library {Compare(null, uris, Id, playlist.Ids)}, songs {shown.Count}→{playlist.Songs.Count}, song order {songOrderOff} off, variant order {OrderMismatches(shown, versions, Id, playlist)} off");
                    if (songOrderOff > 0) Fail($"{p}: the shared library's songs are out of the old order in {songOrderOff} places");
                }
            }

            // the personal playlist
            var personalTenants = scope.PersonalTenants.Where(x => x.Value == p).Select(x => x.Key).ToHashSet();
            var ownSongs = scope.OwnSongsOnly.GetValueOrDefault(p);
            if (personalTenants.Count > 0 || ownSongs != null)
            {
                var shown = old.Songs.Where(x => (personalTenants.Contains(x.TenantId) || ownSongs?.Contains(x.Id) == true) && OldRules.IsShown(x)).ToList();
                var beyond = shown.SelectMany(x => versions[x.Id]).Where(x => OldRules.IsVisible(x, p)).Select(x => x.Uri).Where(x => Id(x) is not { } id || !templateIds.Contains(id)).ToList();
                var copies = shown.Count(x => OldRules.IsTemplateCopy(x, tenants[x.TenantId]));
                var playlist = await PlaylistAsync(Planner.PersonalKey(p));
                var has = playlist?.Ids ?? new HashSet<string>(StringComparer.Ordinal);
                line.Add($"own playlist {Compare(null, beyond, Id, has)} (songs {shown.Count}, of them template copies {copies}; {playlist?.Songs.Count ?? 0} songs there)");
                if (has.Overlaps(templateIds)) Fail($"{p}: the personal playlist has template chords");
            }

            // the lists
            foreach (var list in old.Lists.Where(x => x.UserId == p && !scope.OwnSongsOnly.ContainsKey(p)))
            {
                var playlist = await PlaylistAsync(Planner.ListKey(list.Id));
                if (playlist == null) { Fail($"{p}: list {list.Id} has no playlist"); continue; }
                if (playlist.Playlist.Title != list.Name) Fail($"{p}: list {list.Id}'s title differs");
                var shared = list.Access != OldListAccess.Private && scope.SharedTenants.ContainsKey(list.TenantId);
                var expectedMembers = (shared ? scope.PeopleOf(list.TenantId).ToList() : new List<long> { p }).Select(x => accounts[x]).ToHashSet();
                if (!expectedMembers.SetEquals(playlist.Members.Keys)) Fail($"{p}: list {list.Id}'s members differ");
                var expectedPolicy = shared && list.Access is OldListAccess.SharedEditMulti or OldListAccess.SharedEditSingle ? PlaylistAddPolicy.AllMembers : PlaylistAddPolicy.Admins;
                if (playlist.Playlist.AddPolicy != expectedPolicy) Fail($"{p}: list {list.Id}'s add policy differs");
                var rows = old.Likes.Where(x => x.ListId == list.Id && OldRules.IsShown(songs[x.SongId])).ToList();
                var uris = rows.SelectMany(r => r.VersionId is { } v
                    ? new[] { versionsById[v] }
                    : versions[r.SongId].Where(x => x.CollectiveType is null or OldCollectiveType.Public || (!shared && x.UserId == p)).ToArray()).Select(x => x.Uri);
                var hadHere = rows.Where(x => x.VersionId != null && x.Transpose is not (null or 0)).Select(x => (id: Id(versionsById[x.VersionId!.Value].Uri), x.Transpose, x.UserId))
                    .Where(x => x.id != null).Select(x => $"{x.UserId}:{x.id}:{x.Transpose}").ToHashSet();
                var hasHere = keys.SelectMany(x => x.Where(k => k.PlaylistId == playlist.Playlist.Id).Select(k => $"{accounts.Single(a => a.Value == k.AccountId).Key}:{k.SourceId}:{k.Semitones}")).ToHashSet();
                if (!hadHere.SetEquals(hasHere)) Fail($"{p}: list {list.Id}'s \"here\" keys had {hadHere.Count}, has {hasHere.Count}");
                line.Add($"list {list.Id} [{list.Access}, {rows.Select(x => x.SongId).Distinct().Count()} songs] {Compare(null, uris, Id, playlist.Ids)}, members {playlist.Members.Count}, here {hasHere.Count}/{hadHere.Count}");
            }

            // comments
            var comments = old.Comments.Where(x => x.UserId == p && scope.IsTakenFor(p, x.SongId) && OldRules.IsShown(songs[x.SongId])).ToList();
            var found = comments.Count(c => notes[account].Any(n => n.Contains(c.Text, StringComparison.Ordinal)));
            if (comments.Count > 0)
            {
                line.Add($"comments {found}/{comments.Count}");
                var waiting = comments.Count(c => c.VersionId is { } v && Id(versionsById[v].Uri) == null);
                if (found + waiting < comments.Count) Fail($"{p}: comments {found}/{comments.Count} ({waiting} waiting)");
            }

            // own chords
            var own = old.OwnChords.Where(x => x.AuthorUserId == p && !x.IsDeleted).ToList();
            if (own.Count > 0)
            {
                var playlist = await PlaylistAsync(Planner.OwnChordsKey(p));
                var uris = own.Select(c => old.Versions.Where(v => v.CollectiveId == c.Id).OrderBy(v => v.Id).FirstOrDefault()?.Uri).OfType<string>();
                line.Add($"own chords {Compare(null, uris, Id, playlist?.Ids ?? new HashSet<string>())} (of {own.Count})");
            }

            // recent
            var visits = old.Visits.Where(x => x.UserId == p && OldRules.Time(x.LastViewedOn) != null).ToList();
            var hadRecent = visits.Select(x => x.Uri != null ? Id(x.Uri) : x.ExternalId).OfType<string>().ToHashSet(StringComparer.Ordinal);
            var hasRecent = recent[account].ToHashSet(StringComparer.Ordinal);
            line.Add($"recent {hadRecent.Intersect(hasRecent).Count()}/{hadRecent.Count}{Waiting(visits.Count(x => x.Uri != null && Id(x.Uri) == null))}");
            if (!hadRecent.IsSubsetOf(hasRecent)) Fail($"{p}: recent missing {hadRecent.Except(hasRecent).Count()}");

            output.WriteLine($"{p} \"{person.Title}\" (tenant {person.TenantId}): {string.Join("; ", line)}");
        }

        output.WriteLine();
        output.WriteLine(_failures == 0 ? "Audit: no failures." : $"Audit: {_failures} failures.");
        return _failures;

        string Compare(string? title, IEnumerable<string> uris, Func<string, string?> id, IReadOnlySet<string> has)
        {
            var list = uris.Distinct(StringComparer.Ordinal).ToList();
            var had = list.Select(id).OfType<string>().ToHashSet(StringComparer.Ordinal);
            var waiting = list.Count(x => id(x) == null);
            var missing = had.Except(has).Count();
            var text = $"{had.Count - missing}/{had.Count} chords{Waiting(waiting)}";
            if (missing > 0) Fail($"{title ?? "chords"}: {missing} missing");
            if (title != null) output.WriteLine($"{title}: {text}");
            return text;
        }
    }

    private static int OrderMismatches(IEnumerable<OldSong> shown, ILookup<int, OldVersion> versions, Func<string, string?> id, PlaylistState playlist)
    {
        var mismatches = 0;
        foreach (var song in playlist.Songs)
        {
            var ids = song.ToList();
            var order = ids.Select(x => shown.SelectMany(s => versions[s.Id]).Where(v => id(v.Uri) == x).Select(v => v.Id).DefaultIfEmpty(int.MaxValue).Min()).ToList();
            if (!order.SequenceEqual(order.Order()))
                mismatches++;
        }

        return mismatches;
    }

    private static string Waiting(int count) => count == 0 ? "" : $", waiting {count}";

    private void Check(bool condition, string what)
    {
        _output.WriteLine($"{what}: {(condition ? "ok" : "FAILED")}");
        if (!condition) _failures++;
    }

    private void Fail(string what)
    {
        _output.WriteLine($"  FAILED {what}");
        _failures++;
    }

    private sealed record PlaylistState(Playlist Playlist, Dictionary<Guid, PlaylistRole> Members, ILookup<Guid, string> Songs, HashSet<string> Ids, List<Guid> Order);
}
