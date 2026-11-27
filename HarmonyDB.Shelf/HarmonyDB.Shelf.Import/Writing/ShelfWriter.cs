using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Import.Planning;
using Microsoft.EntityFrameworkCore;
using O = HarmonyDB.Shelf.Import.Report.Outcomes;

namespace HarmonyDB.Shelf.Import.Writing;

/// <summary>
/// Writes a plan into the shelf's database through its model, by its rules, in the caller's transaction.
/// <list type="bullet">
/// <item>Every thing is found by its key in the <see cref="Ledger"/>. Not there: made (or, when the shelf already has it,
/// taken as it is), and recorded. There: what it was made into is looked at. Removed since (a song, a variant, a mark, a
/// membership …), or a whole account deleted: left removed, and nothing is added under it. Still as written, and the old
/// shelf has a new value for it: updated. Changed since by a person: left as it is.</item>
/// <item>Nothing is removed: what the old shelf no longer has stays.</item>
/// <item>A playlist's owner is its owner and the owner's member row; a system playlist is one per person and kind;
/// chords are in a playlist once; a song is made only with a variant; new songs and variants come after the others.</item>
/// <item>Rows it did not make are never changed, except that a recent row of the person's gets the later time.</item>
/// </list>
/// </summary>
public sealed class ShelfWriter(ShelfDbContext db, Ledger ledger, Report report, DateTimeOffset now)
{
    public const string GrantedBy = "tool:import";
    public const string TelegramProvider = "telegram";

    /// <summary>
    /// The accounts of the people, made or found; the people whose account was deleted are not in it.
    /// </summary>
    public async Task<Dictionary<long, Guid>> WritePeopleAsync(IReadOnlyList<PersonPlan> people, CancellationToken cancellationToken)
    {
        var accounts = new Dictionary<long, Guid>();
        var subjects = people.Select(x => x.TelegramId.ToString()).ToList();
        var logins = await db.AccountLogins.Where(x => x.Provider == TelegramProvider && subjects.Contains(x.Subject)).ToDictionaryAsync(x => x.Subject, cancellationToken);
        var recordedIds = people.Select(x => ledger.Get(AccountKey(x.TelegramId))?.Id).OfType<Guid>().ToList();
        var recorded = await db.Accounts.Where(x => recordedIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var person in people)
        {
            var key = AccountKey(person.TelegramId);
            Guid accountId;
            if (ledger.Get(key) is { Id: { } id } entry)
            {
                if (!recorded.TryGetValue(id, out var account) || account.DeletedAt != null)
                {
                    // merged into another account: its sign-ins went there, and so does what is still to come
                    if (logins.TryGetValue(person.TelegramId.ToString(), out var moved) && moved.AccountId != id
                        && await db.Accounts.AnyAsync(x => x.Id == moved.AccountId && x.DeletedAt == null, cancellationToken))
                    {
                        accountId = moved.AccountId;
                        ledger.Set(key, accountId, null, entry.Made);
                        report.Count("account", O.FollowedMerge);
                    }
                    else
                    {
                        report.Count("account", O.LeftByPerson);
                        continue;
                    }
                }
                else
                {
                    accountId = id;
                    report.Count("account", O.Unchanged);
                }
            }
            else if (logins.TryGetValue(person.TelegramId.ToString(), out var login))
            {
                accountId = login.AccountId;
                ledger.Set(key, accountId, null, made: false);
                report.Count("account", O.Found);
            }
            else
            {
                accountId = Guid.NewGuid();
                var createdAt = person.CreatedAt ?? now;
                db.Accounts.Add(new() { Id = accountId, DisplayName = person.DisplayName, CreatedAt = createdAt });
                db.AccountLogins.Add(new()
                {
                    Id = Guid.NewGuid(),
                    AccountId = accountId,
                    Provider = TelegramProvider,
                    Subject = person.TelegramId.ToString(),
                    DisplayName = person.DisplayName,
                    CreatedAt = createdAt,
                    LastSignInAt = createdAt,
                });
                ledger.Set(key, accountId, null);
                report.Count("account", O.Created);
            }

            accounts[person.TelegramId] = accountId;
        }

        await db.SaveChangesAsync(cancellationToken);

        var accountIds = accounts.Values.ToList();
        var granted = (await db.AccountPermissions.Where(x => accountIds.Contains(x.AccountId)).Select(x => new { x.AccountId, x.Permission }).ToListAsync(cancellationToken))
            .Select(x => (x.AccountId, x.Permission)).ToHashSet();
        foreach (var person in people.Where(x => accounts.ContainsKey(x.TelegramId)))
        {
            var accountId = accounts[person.TelegramId];
            foreach (var permission in person.Permissions)
            {
                var key = $"permission:{person.TelegramId}:{permission}";
                if (ledger.Get(key) != null)
                {
                    report.Count("permission", granted.Contains((accountId, permission)) ? O.Unchanged : O.LeftByPerson);
                    continue;
                }

                if (granted.Contains((accountId, permission)))
                {
                    ledger.Set(key, null, null, made: false);
                    report.Count("permission", O.Found);
                }
                else
                {
                    db.AccountPermissions.Add(new() { AccountId = accountId, Permission = permission, GrantedAt = now, GrantedBy = GrantedBy });
                    ledger.Set(key, null, null);
                    report.Count("permission", O.Created);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return accounts;
    }

    /// <summary>
    /// The people's accounts as an earlier run recorded them (for a run that writes nothing new about people).
    /// </summary>
    public static string AccountKey(long telegramId) => $"account:{telegramId}";

    public async Task WriteAsync(ImportPlan plan, IReadOnlyDictionary<long, Guid> accounts, CancellationToken cancellationToken)
    {
        var chords = await ChordsAsync(plan, cancellationToken);
        var playlists = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var songs = new Dictionary<(string playlist, string song), Guid>();
        var items = new Dictionary<(Guid song, Guid chords), Guid>();

        foreach (var playlist in plan.Playlists)
            await WritePlaylistAsync(playlist, accounts, chords, playlists, songs, items, cancellationToken);

        await WriteMarksAsync(plan.Marks, accounts, chords, cancellationToken);
        await WriteKeysAsync(plan.Keys, accounts, chords, playlists, cancellationToken);
        await WriteNotesAsync(plan.Notes, accounts, chords, songs, cancellationToken);
        await WriteRecentAsync(plan.Recent, accounts, chords, cancellationToken);
        await WritePreferencesAsync(plan.Preferences, accounts, chords, songs, items, cancellationToken);
        await ledger.SaveAsync(now, cancellationToken);
    }

    /// <summary>
    /// The local rows of every chords of the plan, made where there are none (as the app makes them: no header yet;
    /// whoever reads the source fills it).
    /// </summary>
    private async Task<Dictionary<string, Guid>> ChordsAsync(ImportPlan plan, CancellationToken cancellationToken)
    {
        var ids = plan.Playlists.SelectMany(x => x.Songs).SelectMany(x => x.Items).Select(x => x.SourceId)
            .Concat(plan.Marks.Select(x => x.SourceId))
            .Concat(plan.Keys.Select(x => x.SourceId))
            .Concat(plan.Notes.Where(x => x.SourceId != null).Select(x => x.SourceId!))
            .Concat(plan.Recent.Select(x => x.SourceId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var existing = await db.Chords.Where(x => ids.Contains(x.SourceId)).Select(x => x.SourceId).ToListAsync(cancellationToken);
        var missing = ids.Except(existing, StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO chords (id, source_id, artists, kinds, created_at)
                 SELECT gen_random_uuid(), x, ARRAY[]::text[], ARRAY[]::text[], {now} FROM unnest({missing}) AS x
                 ON CONFLICT (source_id) DO NOTHING
                 """,
                cancellationToken);
        }

        report.Count("chords", O.Created, missing.Length);
        report.Count("chords", O.Found, existing.Count);

        // the old address each was resolved by (own chords have none in the plan)
        var addressed = ids.Where(plan.Addresses.ContainsKey).ToArray();
        if (addressed.Length > 0)
        {
            var written = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 UPDATE chords SET old_address = x.uri
                 FROM unnest({addressed}, {addressed.Select(x => plan.Addresses[x]).ToArray()}) AS x(id, uri)
                 WHERE chords.source_id = x.id AND chords.old_address IS DISTINCT FROM x.uri
                 """,
                cancellationToken);
            report.Count("chords old address", O.Updated, written);
        }

        return await db.Chords.Where(x => ids.Contains(x.SourceId)).ToDictionaryAsync(x => x.SourceId, x => x.Id, StringComparer.Ordinal, cancellationToken);
    }

    private async Task WritePlaylistAsync(
        PlaylistPlan plan,
        IReadOnlyDictionary<long, Guid> accounts,
        IReadOnlyDictionary<string, Guid> chords,
        Dictionary<string, Guid> playlists,
        Dictionary<(string playlist, string song), Guid> songs,
        Dictionary<(Guid song, Guid chords), Guid> items,
        CancellationToken cancellationToken)
    {
        var kind = plan.SystemKind == PlaylistSystemKind.None ? "playlist" : "system playlist";
        if (!accounts.TryGetValue(plan.OwnerTelegramId, out var owner))
        {
            report.Count(kind, O.NoTarget);
            return;
        }

        var key = $"playlist:{plan.Key}";
        Playlist playlist;
        if (ledger.Get(key) is { Id: { } id } entry)
        {
            var found = await db.Playlists.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (found == null || found.DeletedAt != null)
            {
                report.Count(kind, O.LeftByPerson);
                return;
            }

            playlist = found;
            if (plan.Title != null && entry.Value != plan.Title && playlist.Title == entry.Value)
            {
                playlist.Title = plan.Title;
                playlist.UpdatedAt = now;
                ledger.Set(key, id, plan.Title);
                report.Count(kind, O.Updated);
            }
            else
            {
                report.Count(kind, O.Unchanged);
            }
        }
        else if (plan.SystemKind != PlaylistSystemKind.None)
        {
            playlist = await SystemPlaylistAsync(owner, plan.SystemKind, kind, cancellationToken);
            ledger.Set(key, playlist.Id, null);
        }
        else
        {
            playlist = new()
            {
                Id = Guid.NewGuid(),
                OwnerId = owner,
                Title = plan.Title!,
                Access = plan.Access,
                AddPolicy = plan.AddPolicy,
                SystemKind = PlaylistSystemKind.None,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Playlists.Add(playlist);
            db.PlaylistMembers.Add(new() { Id = Guid.NewGuid(), PlaylistId = playlist.Id, AccountId = owner, Role = PlaylistRole.Owner, JoinedAt = now });
            ledger.Set(key, playlist.Id, plan.Title);
            report.Count(kind, O.Created);
        }

        playlists[plan.Key] = playlist.Id;
        await WriteMembersAsync(plan, playlist, accounts, cancellationToken);
        await WriteSongsAsync(plan, playlist, chords, accounts, songs, items, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The person's system playlist of the kind, made if it is not there (one per person and kind; the same row the app
    /// makes when first needed).
    /// </summary>
    private async Task<Playlist> SystemPlaylistAsync(Guid owner, PlaylistSystemKind systemKind, string kind, CancellationToken cancellationToken)
    {
        var existing = await db.Playlists.SingleOrDefaultAsync(x => x.OwnerId == owner && x.SystemKind == systemKind, cancellationToken);
        if (existing != null)
        {
            report.Count(kind, O.Found);
            return existing;
        }

        var name = SystemPlaylistTitle(systemKind);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             WITH p AS (
                 INSERT INTO playlists (id, owner_id, title, access, add_policy, system_kind, created_at, updated_at)
                 VALUES (gen_random_uuid(), {owner}, {name}, 'private', 'admins', {name}, {now}, {now})
                 ON CONFLICT (owner_id, system_kind) WHERE system_kind <> 'none' AND deleted_at IS NULL DO NOTHING
                 RETURNING id)
             INSERT INTO playlist_members (id, playlist_id, account_id, role, show_in_library, joined_at)
             SELECT gen_random_uuid(), p.id, {owner}, 'owner', TRUE, {now} FROM p
             """,
            cancellationToken);
        report.Count(kind, O.Created);
        return await db.Playlists.SingleAsync(x => x.OwnerId == owner && x.SystemKind == systemKind, cancellationToken);
    }

    /// <summary>
    /// The stored title of a system playlist: its kind as the database keeps it (the app's rule; the frontend words it).
    /// </summary>
    public static string SystemPlaylistTitle(PlaylistSystemKind kind) => kind switch
    {
        PlaylistSystemKind.ImportedChords => "imported_chords",
        PlaylistSystemKind.OwnChords => "own_chords",
        PlaylistSystemKind.OwnAnalyses => "own_analyses",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private async Task WriteMembersAsync(PlaylistPlan plan, Playlist playlist, IReadOnlyDictionary<long, Guid> accounts, CancellationToken cancellationToken)
    {
        var members = await db.PlaylistMembers.Where(x => x.PlaylistId == playlist.Id).Select(x => x.AccountId).ToListAsync(cancellationToken);
        foreach (var member in plan.Members)
        {
            if (!accounts.TryGetValue(member.TelegramId, out var account))
            {
                report.Count("member", O.NoTarget);
                continue;
            }

            var key = $"member:{plan.Key}:{member.TelegramId}";
            if (ledger.Get(key) != null)
            {
                report.Count("member", members.Contains(account) ? O.Unchanged : O.LeftByPerson);
                continue;
            }

            if (members.Contains(account))
            {
                ledger.Set(key, null, null, made: false);
                report.Count("member", O.Found);
            }
            else
            {
                db.PlaylistMembers.Add(new() { Id = Guid.NewGuid(), PlaylistId = playlist.Id, AccountId = account, Role = member.Role, JoinedAt = now });
                ledger.Set(key, null, null);
                report.Count("member", O.Created);
            }
        }
    }

    private async Task WriteSongsAsync(
        PlaylistPlan plan,
        Playlist playlist,
        IReadOnlyDictionary<string, Guid> chords,
        IReadOnlyDictionary<long, Guid> accounts,
        Dictionary<(string playlist, string song), Guid> songs,
        Dictionary<(Guid song, Guid chords), Guid> items,
        CancellationToken cancellationToken)
    {
        var allSongs = await db.PlaylistSongs.IgnoreQueryFilters().Where(x => x.PlaylistId == playlist.Id).ToDictionaryAsync(x => x.Id, cancellationToken);
        var allItems = await db.PlaylistSongItems.IgnoreQueryFilters().Where(x => x.Song.PlaylistId == playlist.Id).ToListAsync(cancellationToken);
        // chords are in a playlist once: in which live song they are
        var present = allItems.Where(x => x.DeletedAt == null && x.ChordsId != null && allSongs[x.SongId].DeletedAt == null)
            .GroupBy(x => x.ChordsId!.Value).ToDictionary(x => x.Key, x => x.First());
        var positions = NewSongPositions(plan, allSongs);

        foreach (var songPlan in plan.Songs)
        {
            var key = $"song:{plan.Key}:{songPlan.Key}";
            PlaylistSong? song = null;
            if (ledger.Get(key) is { Id: { } id } entry)
            {
                if (!allSongs.TryGetValue(id, out song) || song.DeletedAt != null)
                {
                    report.Count("song", O.LeftByPerson);
                    continue;
                }

                var value = SongValue(songPlan.Title, songPlan.Artists);
                if (entry.Value != value && SongValue(song.Title, song.Artists) == entry.Value)
                {
                    song.Title = songPlan.Title;
                    song.Artists = songPlan.Artists.ToList();
                    ledger.Set(key, id, value);
                    report.Count("song", O.Updated);
                }
                else
                {
                    report.Count("song", O.Unchanged);
                }
            }

            var nextItemPosition = song == null ? 0 : allItems.Where(x => x.SongId == song.Id && x.DeletedAt == null).Select(x => x.Position + 1).DefaultIfEmpty(0).Max();
            foreach (var itemPlan in songPlan.Items)
            {
                var chordsId = chords[itemPlan.SourceId];
                var itemKey = $"variant:{plan.Key}:{songPlan.Key}:{itemPlan.SourceId}";
                if (ledger.Get(itemKey) is { Id: { } itemId })
                {
                    var item = allItems.SingleOrDefault(x => x.Id == itemId);
                    if (item == null || item.DeletedAt != null || allSongs[item.SongId].DeletedAt != null)
                    {
                        report.Count("variant", O.LeftByPerson);
                        continue;
                    }

                    items[(item.SongId, chordsId)] = item.Id;
                    // the old version's address and privacy, into an entry made before they were carried
                    if (item.Address != itemPlan.Address || item.Private != itemPlan.Private)
                    {
                        item.Address = itemPlan.Address;
                        item.Private = itemPlan.Private;
                        report.Count("variant", O.Updated);
                    }
                    else
                    {
                        report.Count("variant", O.Unchanged);
                    }

                    continue;
                }

                if (present.TryGetValue(chordsId, out var there))
                {
                    // the chords are already in this playlist (put there by a person, or in another song): taken as they are
                    items[(there.SongId, chordsId)] = there.Id;
                    ledger.Set(itemKey, there.Id, null, made: false);
                    report.Count("variant", O.Found);
                    continue;
                }

                if (song == null)
                {
                    song = new()
                    {
                        Id = Guid.NewGuid(),
                        PlaylistId = playlist.Id,
                        Title = songPlan.Title,
                        Artists = songPlan.Artists.ToList(),
                        Position = positions[songPlan.Key],
                        AddedById = Account(accounts, songPlan.AddedBy),
                        AddedAt = songPlan.AddedAt ?? now,
                    };
                    db.PlaylistSongs.Add(song);
                    allSongs[song.Id] = song;
                    ledger.Set(key, song.Id, SongValue(songPlan.Title, songPlan.Artists));
                    report.Count("song", O.Created);
                }

                var created = new PlaylistSongItem
                {
                    Id = Guid.NewGuid(),
                    SongId = song.Id,
                    ChordsId = chordsId,
                    Position = nextItemPosition++,
                    AddedById = Account(accounts, itemPlan.AddedBy),
                    AddedAt = itemPlan.AddedAt ?? now,
                    Address = itemPlan.Address,
                    Private = itemPlan.Private,
                };
                db.PlaylistSongItems.Add(created);
                allItems.Add(created);
                present[chordsId] = created;
                items[(song.Id, chordsId)] = created.Id;
                ledger.Set(itemKey, created.Id, null);
                report.Count("variant", O.Created);
            }

            if (song != null)
            {
                songs[(plan.Key, songPlan.Key)] = song.Id;
                PutVariantsInOrder(plan, songPlan, song, allSongs, allItems);
            }
        }
    }

    /// <summary>
    /// A variant resolved by a later run was added after the others; in a song whose variants are all as the import made
    /// them (none added or removed by a person since) the variants are put in the old order, so that the letters come out
    /// as they would have with everything resolved at once. A song a person has changed keeps its variants where they are.
    /// </summary>
    private void PutVariantsInOrder(PlaylistPlan plan, SongPlan songPlan, PlaylistSong song, IReadOnlyDictionary<Guid, PlaylistSong> allSongs, List<PlaylistSongItem> allItems)
    {
        var live = allItems.Where(x => x.SongId == song.Id && x.DeletedAt == null).ToList();
        var ours = songPlan.Items
            .Select(x => ledger.Get($"variant:{plan.Key}:{songPlan.Key}:{x.SourceId}"))
            .Where(x => x is { Id: not null })
            .ToList();
        var touched = ours.Any(x => !x!.Made || allItems.SingleOrDefault(i => i.Id == x.Id) is not { DeletedAt: null } item || item.SongId != song.Id || allSongs[item.SongId].DeletedAt != null)
                      || live.Any(x => ours.All(o => o!.Id != x.Id));
        if (touched)
            return;

        var ordered = ours.Select(x => live.Single(i => i.Id == x!.Id)).ToList();
        if (ordered.Select(x => x.Position).SequenceEqual(ordered.Select(x => x.Position).Order()))
            return;

        for (var i = 0; i < ordered.Count; i++)
            ordered[i].Position = i;
        report.Count("variant", O.Reordered);
    }

    /// <summary>
    /// The positions of the songs of the plan that are not there yet: each run of them goes where the plan has it, after
    /// the last song before it in the plan that is there and before the next song of the playlist, spread evenly in that
    /// gap.
    /// </summary>
    private Dictionary<string, double> NewSongPositions(PlaylistPlan plan, IReadOnlyDictionary<Guid, PlaylistSong> allSongs)
    {
        var live = allSongs.Values.Where(x => x.DeletedAt == null).Select(x => x.Position).Order().ToList();
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        var run = new List<string>();
        double? previous = null;

        void Place(double? next)
        {
            var nextOrAfter = next ?? live.Where(x => previous == null || x > previous).DefaultIfEmpty(double.NaN).Min();
            for (var j = 0; j < run.Count; j++)
            {
                result[run[j]] = (previous, double.IsNaN(nextOrAfter) ? (double?)null : nextOrAfter) switch
                {
                    (null, null) => j + 1,
                    (null, { } n) => n - (run.Count - j),
                    ({ } p, null) => p + j + 1,
                    ({ } p, { } n) => p + (n - p) * (j + 1) / (run.Count + 1),
                };
            }

            run.Clear();
        }

        foreach (var songPlan in plan.Songs)
        {
            var there = ledger.Get($"song:{plan.Key}:{songPlan.Key}") is { Id: { } id } ? allSongs.GetValueOrDefault(id) : null;
            if (there == null)
            {
                if (ledger.Get($"song:{plan.Key}:{songPlan.Key}") == null)
                    run.Add(songPlan.Key);
                continue;
            }

            if (there.DeletedAt != null)
                continue;

            Place(live.Where(x => previous == null || x > previous).Where(x => x <= there.Position).DefaultIfEmpty(there.Position).Min());
            previous = there.Position;
        }

        Place(null);
        return result;
    }

    /// <summary>
    /// The letter of a variant by its place among the song's chords: A … Z, then AA, AB …
    /// </summary>
    internal static string Letter(int index) => index < 26 ? ((char)('A' + index)).ToString() : Letter(index / 26 - 1) + Letter(index % 26);

    private static string SongValue(string title, IEnumerable<string> artists) => string.Join('\u001f', artists.Prepend(title));

    private static Guid? Account(IReadOnlyDictionary<long, Guid> accounts, long? person)
        => person is { } id && accounts.TryGetValue(id, out var account) ? account : null;

    private async Task WriteMarksAsync(IReadOnlyList<MarkPlan> marks, IReadOnlyDictionary<long, Guid> accounts, IReadOnlyDictionary<string, Guid> chords, CancellationToken cancellationToken)
    {
        var accountIds = accounts.Values.ToList();
        var existing = await db.Marks.IgnoreQueryFilters().Where(x => accountIds.Contains(x.AccountId)).ToListAsync(cancellationToken);
        var byId = existing.ToDictionary(x => x.Id);
        foreach (var plan in marks)
        {
            if (!accounts.TryGetValue(plan.TelegramId, out var account))
            {
                report.Count("heart", O.NoTarget);
                continue;
            }

            var chordsId = chords[plan.SourceId];
            var key = $"mark:{plan.TelegramId}:{plan.SourceId}";
            if (ledger.Get(key) is { Id: { } id } entry)
            {
                if (!entry.Made)
                {
                    report.Count("heart", O.Unchanged);
                    continue;
                }

                if (!byId.TryGetValue(id, out var mark) || mark.DeletedAt != null || mark.Kind != entry.Value)
                {
                    report.Count("heart", O.LeftByPerson);
                    continue;
                }

                if (mark.Kind == plan.Kind)
                {
                    if (plan.At is { } at && mark.CreatedAt != at)
                    {
                        mark.CreatedAt = at;
                        report.Count("heart", O.Dated);
                    }
                    else
                    {
                        report.Count("heart", O.Unchanged);
                    }

                    continue;
                }

                mark.DeletedAt = now;
                var replaced = Live(existing, account, chordsId, plan.Kind) ?? NewMark(account, chordsId, plan.Kind, plan.At, existing);
                ledger.Set(key, replaced.Id, plan.Kind);
                report.Count("heart", O.Updated);
                continue;
            }

            if (Live(existing, account, chordsId, plan.Kind) is { } found)
            {
                ledger.Set(key, found.Id, plan.Kind, made: false);
                report.Count("heart", O.Found);
                continue;
            }

            ledger.Set(key, NewMark(account, chordsId, plan.Kind, plan.At, existing).Id, plan.Kind);
            report.Count("heart", O.Created);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Mark? Live(List<Mark> marks, Guid account, Guid chordsId, string kind)
        => marks.FirstOrDefault(x => x.AccountId == account && x.ChordsId == chordsId && x.Kind == kind && x.DeletedAt == null);

    private Mark NewMark(Guid account, Guid chordsId, string kind, DateTimeOffset? at, List<Mark> marks)
    {
        var mark = new Mark { Id = Guid.NewGuid(), AccountId = account, ChordsId = chordsId, Kind = kind, CreatedAt = at ?? now };
        db.Marks.Add(mark);
        marks.Add(mark);
        return mark;
    }

    private async Task WriteKeysAsync(IReadOnlyList<KeyPlan> keys, IReadOnlyDictionary<long, Guid> accounts, IReadOnlyDictionary<string, Guid> chords, IReadOnlyDictionary<string, Guid> playlists, CancellationToken cancellationToken)
    {
        var accountIds = accounts.Values.ToList();
        var existing = await db.Transpositions.IgnoreQueryFilters().Where(x => accountIds.Contains(x.AccountId)).ToListAsync(cancellationToken);
        var byId = existing.ToDictionary(x => x.Id);
        foreach (var plan in keys)
        {
            var kind = plan.PlaylistKey == null ? "key mine" : "key here";
            Guid? playlistId = null;
            if (!accounts.TryGetValue(plan.TelegramId, out var account) || (plan.PlaylistKey != null && !playlists.TryGetValue(plan.PlaylistKey, out var p)))
            {
                report.Count(kind, O.NoTarget);
                continue;
            }

            if (plan.PlaylistKey != null)
                playlistId = playlists[plan.PlaylistKey];

            var chordsId = chords[plan.SourceId];
            var key = $"key:{plan.TelegramId}:{plan.SourceId}:{plan.PlaylistKey ?? "mine"}";
            var value = plan.Semitones.ToString();
            if (ledger.Get(key) is { Id: { } id } entry)
            {
                if (!entry.Made)
                    report.Count(kind, O.Unchanged);
                else if (!byId.TryGetValue(id, out var row) || row.DeletedAt != null || row.Semitones.ToString() != entry.Value)
                    report.Count(kind, O.LeftByPerson);
                else if (entry.Value != value)
                {
                    row.Semitones = plan.Semitones;
                    row.UpdatedAt = plan.At ?? now;
                    ledger.Set(key, id, value);
                    report.Count(kind, O.Updated);
                }
                else if (plan.At is { } at && row.UpdatedAt != at)
                {
                    row.UpdatedAt = at;
                    report.Count(kind, O.Dated);
                }
                else
                {
                    report.Count(kind, O.Unchanged);
                }

                continue;
            }

            if (existing.FirstOrDefault(x => x.AccountId == account && x.ChordsId == chordsId && x.PlaylistId == playlistId && x.DeletedAt == null) is { } found)
            {
                // the person set their own key there in the new shelf: it is theirs, never changed by the import
                ledger.Set(key, found.Id, null, made: false);
                report.Count(kind, O.Found);
                continue;
            }

            var created = new Transposition { Id = Guid.NewGuid(), AccountId = account, ChordsId = chordsId, PlaylistId = playlistId, Semitones = plan.Semitones, UpdatedAt = plan.At ?? now };
            db.Transpositions.Add(created);
            existing.Add(created);
            ledger.Set(key, created.Id, value);
            report.Count(kind, O.Created);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task WriteNotesAsync(IReadOnlyList<NotePlan> notes, IReadOnlyDictionary<long, Guid> accounts, IReadOnlyDictionary<string, Guid> chords, IReadOnlyDictionary<(string playlist, string song), Guid> songs, CancellationToken cancellationToken)
    {
        var accountIds = accounts.Values.ToList();
        var existing = await db.Notes.IgnoreQueryFilters().Where(x => accountIds.Contains(x.AccountId)).ToListAsync(cancellationToken);
        var byId = existing.ToDictionary(x => x.Id);
        var noteSongs = notes.Where(x => x.SongKey != null && songs.ContainsKey((x.PlaylistKey!, x.SongKey))).Select(x => songs[(x.PlaylistKey!, x.SongKey!)]).Distinct().ToList();
        var letters = (await db.PlaylistSongItems.Where(x => noteSongs.Contains(x.SongId) && x.ChordsId != null)
                .Select(x => new { x.SongId, ChordsId = x.ChordsId!.Value, x.Position, x.Id }).ToListAsync(cancellationToken))
            .GroupBy(x => x.SongId)
            .ToDictionary(x => x.Key, x => x.OrderBy(i => i.Position).ThenBy(i => i.Id).Select((i, n) => (i.ChordsId, n)).ToDictionary(i => i.ChordsId, i => Letter(i.n)));
        foreach (var plan in notes)
        {
            var kind = plan.SongKey != null ? "note in playlist" : "note personal";
            Guid? songId = null, chordsId = null;
            if (!accounts.TryGetValue(plan.TelegramId, out var account)
                || (plan.SongKey != null && !songs.ContainsKey((plan.PlaylistKey!, plan.SongKey))))
            {
                report.Count(kind, O.NoTarget);
                continue;
            }

            if (plan.SongKey != null)
                songId = songs[(plan.PlaylistKey!, plan.SongKey)];
            else
                chordsId = chords[plan.SourceId!];

            // on a song of several variants, a comment on one begins with its letter as it stands
            var songLetters = songId is { } s ? letters.GetValueOrDefault(s) : null;
            var text = string.Join("\n\n", plan.Parts.Select(x =>
                x.SourceId != null && songLetters is { Count: > 1 } && songLetters.TryGetValue(chords[x.SourceId], out var letter) ? $"{letter}: {x.Text}" : x.Text));

            var key = plan.SongKey != null ? $"note:{plan.TelegramId}:{plan.PlaylistKey}:{plan.SongKey}" : $"note:{plan.TelegramId}:{plan.SourceId}";
            if (ledger.Get(key) is { Id: { } id } entry)
            {
                if (!entry.Made)
                    report.Count(kind, O.Unchanged);
                else if (!byId.TryGetValue(id, out var row) || row.DeletedAt != null || row.Text != entry.Value)
                    report.Count(kind, O.LeftByPerson);
                else if (entry.Value != text)
                {
                    row.Text = text;
                    row.UpdatedAt = plan.At ?? now;
                    ledger.Set(key, id, text);
                    report.Count(kind, O.Updated);
                }
                else if (plan.At is { } at && row.UpdatedAt != at)
                {
                    row.UpdatedAt = at;
                    report.Count(kind, O.Dated);
                }
                else
                {
                    report.Count(kind, O.Unchanged);
                }

                continue;
            }

            if (existing.FirstOrDefault(x => x.AccountId == account && x.SongId == songId && x.ChordsId == chordsId && x.DeletedAt == null) is { } found)
            {
                // the person wrote their own note there in the new shelf: it is theirs, never changed by the import
                ledger.Set(key, found.Id, null, made: false);
                report.Count(kind, O.Found);
                continue;
            }

            var created = new Note { Id = Guid.NewGuid(), AccountId = account, SongId = songId, ChordsId = chordsId, Text = text, UpdatedAt = plan.At ?? now };
            db.Notes.Add(created);
            existing.Add(created);
            ledger.Set(key, created.Id, text);
            report.Count(kind, O.Created);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// A recent row named as the old shelf's visit was: its title, artists and source as the person was shown them.
    /// </summary>
    private static void Name(RecentItem row, RecentPlan plan)
    {
        row.Title = plan.Title;
        row.Artists = plan.Artists?.ToList();
        row.SourceTitle = plan.Source;
    }

    private async Task WriteRecentAsync(IReadOnlyList<RecentPlan> recent, IReadOnlyDictionary<long, Guid> accounts, IReadOnlyDictionary<string, Guid> chords, CancellationToken cancellationToken)
    {
        var accountIds = accounts.Values.ToList();
        var existing = await db.Recent.IgnoreQueryFilters().Where(x => accountIds.Contains(x.AccountId)).ToListAsync(cancellationToken);
        var byId = existing.ToDictionary(x => x.Id);
        foreach (var plan in recent)
        {
            if (!accounts.TryGetValue(plan.TelegramId, out var account))
            {
                report.Count("recent", O.NoTarget);
                continue;
            }

            var chordsId = chords[plan.SourceId];
            var key = $"recent:{plan.TelegramId}:{plan.SourceId}";
            if (ledger.Get(key) is { Id: { } id })
            {
                if (!byId.TryGetValue(id, out var row) || row.DeletedAt != null)
                    report.Count("recent", O.LeftByPerson);
                else if (row.OpenedAt > plan.OpenedAt || (row.OpenedAt == plan.OpenedAt && row.Title != null))
                    report.Count("recent", O.Unchanged);
                else
                {
                    row.OpenedAt = plan.OpenedAt;
                    Name(row, plan);
                    report.Count("recent", O.Updated);
                }

                continue;
            }

            if (existing.FirstOrDefault(x => x.AccountId == account && x.ChordsId == chordsId && x.SongId == null && x.DeletedAt == null) is { } found)
            {
                if (found.OpenedAt < plan.OpenedAt)
                {
                    found.OpenedAt = plan.OpenedAt;
                    Name(found, plan);
                }

                ledger.Set(key, found.Id, null, made: false);
                report.Count("recent", O.Found);
                continue;
            }

            var created = new RecentItem { Id = Guid.NewGuid(), AccountId = account, ChordsId = chordsId, OpenedAt = plan.OpenedAt };
            Name(created, plan);
            db.Recent.Add(created);
            existing.Add(created);
            ledger.Set(key, created.Id, null);
            report.Count("recent", O.Created);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task WritePreferencesAsync(
        IReadOnlyList<PreferencePlan> preferences,
        IReadOnlyDictionary<long, Guid> accounts,
        IReadOnlyDictionary<string, Guid> chords,
        IReadOnlyDictionary<(string playlist, string song), Guid> songs,
        IReadOnlyDictionary<(Guid song, Guid chords), Guid> items,
        CancellationToken cancellationToken)
    {
        var songIds = songs.Values.ToList();
        var existing = (await db.PlaylistSongPreferences.IgnoreQueryFilters().Where(x => songIds.Contains(x.SongId)).Select(x => new { x.AccountId, x.SongId }).ToListAsync(cancellationToken))
            .Select(x => (x.AccountId, x.SongId)).ToHashSet();
        // the first variant of each song as it stands now: a main variant is kept only where it is another
        var first = (await db.PlaylistSongItems.Where(x => songIds.Contains(x.SongId) && x.ChordsId != null)
                .Select(x => new { x.SongId, x.Id, x.Position }).ToListAsync(cancellationToken))
            .GroupBy(x => x.SongId).ToDictionary(x => x.Key, x => x.OrderBy(i => i.Position).ThenBy(i => i.Id).First().Id);
        foreach (var plan in preferences)
        {
            if (!accounts.TryGetValue(plan.TelegramId, out var account)
                || !songs.TryGetValue((plan.PlaylistKey, plan.SongKey), out var songId)
                || !items.TryGetValue((songId, chords[plan.SourceId]), out var itemId))
            {
                report.Count("main variant", O.NoTarget);
                continue;
            }

            if (first.GetValueOrDefault(songId) == itemId)
                continue;

            var key = $"main:{plan.TelegramId}:{plan.PlaylistKey}:{plan.SongKey}";
            if (ledger.Get(key) != null)
            {
                report.Count("main variant", O.Unchanged);
                continue;
            }

            if (existing.Contains((account, songId)))
            {
                ledger.Set(key, null, null, made: false);
                report.Count("main variant", O.Found);
            }
            else
            {
                db.PlaylistSongPreferences.Add(new() { AccountId = account, SongId = songId, MainItemId = itemId });
                existing.Add((account, songId));
                ledger.Set(key, null, null);
                report.Count("main variant", O.Created);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
