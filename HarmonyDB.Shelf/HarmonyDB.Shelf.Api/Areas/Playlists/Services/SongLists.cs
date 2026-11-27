using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using System.Security.Claims;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Chords;
using Microsoft.EntityFrameworkCore;
using M = HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Api.Areas.Playlists.Services;

/// <summary>
/// What a list of songs is asked with: a text (live search over the songs' and their chords' titles and artists), who
/// marked (<c>me</c>, or a person's id; with <see cref="Mark"/>, nobody given means anyone), the kind of mark, the sort,
/// and the page.
/// </summary>
public sealed record SongListQuery(string? Q, M.SongSort? Sort, string? Who, string? Mark, int Offset, int Limit)
{
    public const int MaxLimit = 1000;

    /// <summary>
    /// The sort as the JSON writes it (<c>newSongs</c> …); null: the list's default.
    /// </summary>
    public static M.SongSort? ParseSort(string? sort)
        => sort == null ? null
            : Enum.TryParse<M.SongSort>(sort, true, out var value) && Enum.IsDefined(value) && !int.TryParse(sort, out _) ? value
            : throw ShelfException.BadRequest();
}

/// <summary>
/// The lists of songs:
/// <list type="bullet">
/// <item>a playlist's: filtered, sorted, counted and paged by the database, so that a playlist of ten thousand songs is
/// read a page at a time, also while the person types;</item>
/// <item>the person's library — everything from their playlists shown in it (a song that several of them hold, by
/// the same chords, once), plus the chords they marked, keep a key or a personal note on, that are in none of those:
/// joined across playlists here, then paged.</item>
/// </list>
/// A list is the shelf's own: no source is asked to show it. A row names its chords by their ids; the person's device
/// keeps their sources' labels (<c>POST /api/chords/labels</c>). What a person sees of a playlist's entries is
/// <see cref="PlaylistSongItemsSeen"/>.
/// </summary>
public sealed class SongLists(ShelfDbContext db, PlaylistGate gate, Views views)
{
    private const int OtherMarksShown = 3;

    public async Task<M.SongList> PlaylistAsync(ClaimsPrincipal user, Guid playlistId, string? share, SongListQuery query, CancellationToken cancellationToken)
    {
        var viewer = user.GetAccountId();
        var view = await gate.ReadAsync(viewer, playlistId, share, cancellationToken);
        var people = view.IsMember
            ? await db.PlaylistMembers.Where(x => x.PlaylistId == playlistId).Select(x => x.AccountId).ToListAsync(cancellationToken)
            : viewer is { } me ? [me] : [];

        var items = db.PlaylistSongItems.SeenBy(viewer);
        var marks = db.Marks.Where(m => people.Contains(m.AccountId));
        var songs = db.PlaylistSongs.Where(s => s.PlaylistId == playlistId).SeenBy(viewer);

        if (Normalize(query.Q) is { Length: > 0 } q)
        {
            var pattern = "%" + PlaylistService.EscapeLike(q) + "%";
            songs = songs.Where(s =>
                EF.Functions.ILike(s.Title.Replace("ё", "е").Replace("Ё", "Е"), pattern)
                || s.Artists.Any(a => EF.Functions.ILike(a.Replace("ё", "е").Replace("Ё", "Е"), pattern))
                || items.Any(i => i.SongId == s.Id && i.Chords != null
                                  && (EF.Functions.ILike(i.Chords.Title!.Replace("ё", "е").Replace("Ё", "Е"), pattern)
                                      || i.Chords.Artists.Any(a => EF.Functions.ILike(a.Replace("ё", "е").Replace("Ё", "Е"), pattern)))));
        }

        var who = Who(query.Who, viewer);
        if (query.Mark != null || who != null)
        {
            var kind = query.Mark;
            // from the song to its chords to their marks: each step by its index
            songs = songs.Where(s => items.Any(i => i.SongId == s.Id
                                                    && marks.Any(m => m.ChordsId == i.ChordsId && (who == null || m.AccountId == who) && (kind == null || m.Kind == kind))));
        }

        var total = await songs.CountAsync(cancellationToken);
        var offset = Math.Max(0, query.Offset);
        var limit = Math.Clamp(query.Limit, 1, SongListQuery.MaxLimit);
        var sort = query.Sort ?? M.SongSort.Order;
        List<PlaylistSong> page;
        if (sort is M.SongSort.Popular or M.SongSort.NewChords or M.SongSort.MyMarks)
        {
            // an order by what is counted over the songs' chords: counted once for the whole playlist, grouped, then the
            // songs that match ordered by it and paged
            var live = items.Where(i => i.Song.PlaylistId == playlistId && i.Song.DeletedAt == null && i.ChordsId != null);
            var weights = sort switch
            {
                M.SongSort.Popular => (await (from m in marks
                            join i in live on m.ChordsId equals i.ChordsId
                            select new { i.SongId, m.AccountId })
                        .Distinct().GroupBy(x => x.SongId).Select(x => new { x.Key, Count = x.Count() })
                        .ToListAsync(cancellationToken))
                    .ToDictionary(x => x.Key, x => (double)x.Count),
                M.SongSort.NewChords => (await live.GroupBy(i => i.SongId).Select(x => new { x.Key, At = x.Max(i => i.AddedAt) }).ToListAsync(cancellationToken))
                    .ToDictionary(x => x.Key, x => (double)x.At.UtcTicks),
                _ => (await (from m in marks.Where(m => m.AccountId == viewer)
                            join i in live on m.ChordsId equals i.ChordsId
                            group m.CreatedAt by i.SongId into g
                            select new { g.Key, At = g.Max() })
                        .ToListAsync(cancellationToken))
                    .ToDictionary(x => x.Key, x => (double)x.At.UtcTicks),
            };
            var keys = await songs.Select(s => new { s.Id, s.AddedAt }).ToListAsync(cancellationToken);
            var ordered = (sort == M.SongSort.Popular
                    ? keys.OrderByDescending(x => weights.GetValueOrDefault(x.Id)).ThenBy(x => x.AddedAt)
                    : keys.OrderByDescending(x => weights.GetValueOrDefault(x.Id, double.MinValue)))
                .ThenBy(x => x.Id)
                .Skip(offset).Take(limit).Select(x => x.Id).ToList();
            var found = await db.PlaylistSongs.Where(s => ordered.Contains(s.Id)).AsNoTracking().ToDictionaryAsync(s => s.Id, cancellationToken);
            page = ordered.Select(x => found[x]).ToList();
        }
        else
        {
            IOrderedQueryable<PlaylistSong> sorted = sort switch
            {
                M.SongSort.Artist => songs.OrderBy(s => s.Artists.Count == 0 ? "" : s.Artists[0].ToLower()).ThenBy(s => s.Title.ToLower()),
                M.SongSort.Title => songs.OrderBy(s => s.Title.ToLower()).ThenBy(s => s.Artists.Count == 0 ? "" : s.Artists[0].ToLower()),
                M.SongSort.NewSongs => songs.OrderByDescending(s => s.AddedAt),
                _ => songs.OrderBy(s => s.Position),
            };
            page = await sorted.ThenBy(s => s.Id).Skip(offset).Take(limit).AsNoTracking().ToListAsync(cancellationToken);
        }

        var data = await SongDataAsync(page, viewer, cancellationToken);
        var entries = data.Select(x => Entry.Of(x, people)).ToList();
        await MarksAsync(entries, people, cancellationToken);
        return new()
        {
            Total = total,
            Offset = offset,
            Rows = await RowsAsync(user, entries, cancellationToken),
            People = await PeopleAsync(viewer, people, cancellationToken),
        };
    }

    public async Task<M.SongList> LibraryAsync(ClaimsPrincipal user, SongListQuery query, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;
        var (memberOf, followed) = await ShownAsync(me, cancellationToken);
        var shown = memberOf.Concat(followed).ToList();
        // whose marks are seen in each: its members', in a playlist one is a member of; one's own, in one followed
        var members = (await db.PlaylistMembers.Where(x => memberOf.Contains(x.PlaylistId)).Select(x => new { x.PlaylistId, x.AccountId }).ToListAsync(cancellationToken))
            .Concat(followed.Select(x => new { PlaylistId = x, AccountId = me }))
            .ToLookup(x => x.PlaylistId, x => x.AccountId);

        // a song held by several shown playlists (by the same chords) is one row: the first playlist's song stands for
        // the others
        var songs = await SongDataAsync(await db.PlaylistSongs.Where(x => shown.Contains(x.PlaylistId)).SeenBy(me).AsNoTracking().ToListAsync(cancellationToken), me, cancellationToken);
        var entries = new List<Entry>();
        var byChords = new Dictionary<Guid, Entry>();
        foreach (var song in songs.OrderBy(x => shown.IndexOf(x.Song.PlaylistId)).ThenBy(x => x.Song.Position).ThenBy(x => x.Song.Id))
        {
            var found = song.Chords.Select(x => byChords.GetValueOrDefault(x.Id)).Where(x => x != null).Distinct().ToList();
            var entry = found.FirstOrDefault();
            if (entry == null)
            {
                entry = Entry.Of(song, members[song.Song.PlaylistId]);
                entries.Add(entry);
            }
            else
            {
                entry.Absorb(song, members[song.Song.PlaylistId]);
                foreach (var other in found.Skip(1))
                {
                    entry.Absorb(other!);
                    entries.Remove(other!);
                }
            }

            foreach (var chords in entry.Chords)
                byChords[chords.Id] = entry;
        }

        // and the chords that are only the person's own
        var own = (await db.Marks.Where(x => x.AccountId == me).Select(x => new { x.ChordsId, At = x.CreatedAt }).ToListAsync(cancellationToken))
            .Concat(await db.Transpositions.Where(x => x.AccountId == me).Select(x => new { x.ChordsId, At = x.UpdatedAt }).ToListAsync(cancellationToken))
            .Concat(await db.Notes.Where(x => x.AccountId == me && x.ChordsId != null).Select(x => new { ChordsId = x.ChordsId!.Value, At = x.UpdatedAt }).ToListAsync(cancellationToken))
            .Where(x => !byChords.ContainsKey(x.ChordsId))
            .GroupBy(x => x.ChordsId)
            .ToDictionary(x => x.Key, x => x.Min(y => y.At));
        var ownIds = own.Keys.ToList();
        entries.AddRange((await db.Chords.Where(x => ownIds.Contains(x.Id)).AsNoTracking().ToListAsync(cancellationToken)).Select(x => Entry.Bare(x, own[x.Id], me)));

        var people = members.SelectMany(x => x).Append(me).Distinct().ToList();
        await MarksAsync(entries, people, cancellationToken);

        IEnumerable<Entry> rows = entries;
        if (Normalize(query.Q) is { Length: > 0 } q)
            rows = rows.Where(x => x.Text.Contains(q, StringComparison.Ordinal));

        var who = Who(query.Who, me);
        if (query.Mark != null)
            rows = rows.Where(x => x.Marks.Any(m => m.Kind == query.Mark && (who == null || m.AccountId == who)));
        else if (who != null)
            rows = rows.Where(x => x.Marks.Any(m => m.AccountId == who));

        rows = (query.Sort ?? M.SongSort.Popular) switch
        {
            M.SongSort.Artist => rows.OrderBy(x => x.SortArtist, StringComparer.Ordinal).ThenBy(x => x.SortTitle, StringComparer.Ordinal),
            M.SongSort.Title => rows.OrderBy(x => x.SortTitle, StringComparer.Ordinal).ThenBy(x => x.SortArtist, StringComparer.Ordinal),
            M.SongSort.NewSongs => rows.OrderByDescending(x => x.AddedAt),
            M.SongSort.NewChords => rows.OrderByDescending(x => x.ChordsAddedAt),
            M.SongSort.MyMarks => rows.OrderByDescending(x => x.Marks.Where(m => m.AccountId == me).Select(m => (DateTimeOffset?)m.CreatedAt).Max()),
            M.SongSort.Order => rows.OrderBy(x => x.Position),
            _ => rows.OrderByDescending(x => x.Marks.Select(m => m.AccountId).Distinct().Count()).ThenBy(x => x.AddedAt),
        };

        var all = rows.ToList();
        var offset = Math.Max(0, query.Offset);
        var page = all.Skip(offset).Take(Math.Clamp(query.Limit, 1, SongListQuery.MaxLimit)).ToList();
        return new()
        {
            Total = all.Count,
            Offset = offset,
            Rows = await RowsAsync(user, page, cancellationToken),
            People = await PeopleAsync(me, people, cancellationToken),
        };
    }

    /// <summary>
    /// The chords of the person's library: of the songs of the playlists shown in it, and the person's own (marked, or
    /// with their key or note); each with the address it is read by (of the latest entry that has one).
    /// </summary>
    public async Task<IReadOnlyList<(ChordsItem Chords, string? Address)>> LibraryChordsAsync(Guid me, CancellationToken cancellationToken)
    {
        var (memberOf, followed) = await ShownAsync(me, cancellationToken);
        var shown = memberOf.Concat(followed).ToList();
        var entries = await db.PlaylistSongItems.SeenBy(me)
            .Where(x => x.ChordsId != null && shown.Contains(x.Song.PlaylistId) && x.Song.DeletedAt == null)
            .Select(x => new { ChordsId = x.ChordsId!.Value, x.Address, x.AddedAt })
            .ToListAsync(cancellationToken);
        var addresses = entries.GroupBy(x => x.ChordsId)
            .ToDictionary(x => x.Key, x => x.Where(e => e.Address != null).OrderByDescending(e => e.AddedAt).Select(e => e.Address).FirstOrDefault());
        var own = await db.Marks.Where(x => x.AccountId == me).Select(x => x.ChordsId)
            .Union(db.Transpositions.Where(x => x.AccountId == me).Select(x => x.ChordsId))
            .Union(db.Notes.Where(x => x.AccountId == me && x.ChordsId != null).Select(x => x.ChordsId!.Value))
            .ToListAsync(cancellationToken);
        var ids = addresses.Keys.Union(own).ToList();
        return (await db.Chords.Where(x => ids.Contains(x.Id)).AsNoTracking().ToListAsync(cancellationToken))
            .Select(x => (x, addresses.GetValueOrDefault(x.Id)))
            .ToList();
    }

    /// <summary>
    /// The playlists shown in the person's library: those they are a member of and show, in the order joined; then those
    /// they follow and show, in the order followed (a follow counts only where the person is no member at all: a member
    /// shows a playlist or not by their membership).
    /// </summary>
    private async Task<(List<Guid> MemberOf, List<Guid> Followed)> ShownAsync(Guid me, CancellationToken cancellationToken)
    {
        var memberOf = await db.PlaylistMembers.Where(x => x.AccountId == me && x.ShowInLibrary && x.Playlist.DeletedAt == null)
            .OrderBy(x => x.JoinedAt).Select(x => x.PlaylistId).ToListAsync(cancellationToken);
        var followed = await db.PlaylistFollows.Where(x => x.AccountId == me && x.ShowInLibrary && x.Playlist.DeletedAt == null && x.Playlist.Access != PlaylistAccess.Private)
            .OrderBy(x => x.FollowedAt).Select(x => x.PlaylistId).ToListAsync(cancellationToken);
        var anyMembership = await db.PlaylistMembers.Where(x => x.AccountId == me).Select(x => x.PlaylistId).ToListAsync(cancellationToken);
        return (memberOf, followed.Except(anyMembership).ToList());
    }

    /// <summary>
    /// A text as the search compares it.
    /// </summary>
    public static string Normalize(string? text) => (text ?? string.Empty).Trim().ToLowerInvariant().Replace('ё', 'е');

    private static Guid? Who(string? who, Guid? viewer) => who switch
    {
        null or "" or "all" => null,
        "me" => viewer ?? Guid.Empty,
        _ => Guid.TryParse(who, out var id) ? id : Guid.Empty,
    };

    /// <summary>
    /// The songs with their variants not removed.
    /// </summary>
    private async Task<List<SongData>> SongDataAsync(IReadOnlyList<PlaylistSong> songs, Guid? viewer, CancellationToken cancellationToken)
    {
        var ids = songs.Select(x => x.Id).ToList();
        var items = await db.PlaylistSongItems.SeenBy(viewer).Where(x => ids.Contains(x.SongId))
            .OrderBy(x => x.Position).ThenBy(x => x.Id)
            .Select(x => new { x.SongId, x.Chords, x.RecordingId, x.AddedAt })
            .AsNoTracking().ToListAsync(cancellationToken);
        var bySong = items.ToLookup(x => x.SongId);
        return songs.Select(song => new SongData(
            song,
            bySong[song.Id].Where(x => x.Chords != null).Select(x => x.Chords!).ToList(),
            bySong[song.Id].Count(x => x.RecordingId != null),
            bySong[song.Id].Where(x => x.Chords != null).Select(x => (DateTimeOffset?)x.AddedAt).Max() ?? song.AddedAt)).ToList();
    }

    /// <summary>
    /// The marks on the entries' chords by the people each entry's marks are seen of.
    /// </summary>
    private async Task MarksAsync(List<Entry> entries, IReadOnlyCollection<Guid> people, CancellationToken cancellationToken)
    {
        var chordsIds = entries.SelectMany(x => x.Chords.Select(c => c.Id)).Distinct().ToList();
        var marks = people.Count == 0 || chordsIds.Count == 0
            ? []
            : await db.Marks.Where(x => people.Contains(x.AccountId) && chordsIds.Contains(x.ChordsId))
                .Select(x => new MarkRow(x.AccountId, x.ChordsId, x.Kind, x.CreatedAt))
                .ToListAsync(cancellationToken);
        var byChords = marks.ToLookup(x => x.ChordsId);
        foreach (var entry in entries)
            entry.Marks = entry.Chords.SelectMany(x => byChords[x.Id]).Where(x => entry.People.Contains(x.AccountId)).ToList();
    }

    private async Task<List<M.SongRow>> RowsAsync(ClaimsPrincipal user, List<Entry> page, CancellationToken cancellationToken)
    {
        var covers = await views.SongCoversAsync(user.GetAccountId(), page.Where(x => x.SongId != null).Select(x => x.SongId!.Value).ToList(), cancellationToken);
        return page.Select(x => x.Row(user.GetAccountId(), covers)).ToList();
    }

    private async Task<List<M.PersonRef>> PeopleAsync(Guid? viewer, IReadOnlyList<Guid> people, CancellationToken cancellationToken)
    {
        var persons = await views.PeopleAsync(people, cancellationToken);
        return people.OrderBy(x => x == viewer ? 0 : 1).Where(persons.ContainsKey).Select(x => persons[x]).ToList();
    }

    private sealed record SongData(PlaylistSong Song, List<ChordsItem> Chords, int Recordings, DateTimeOffset ChordsAddedAt);

    private sealed record MarkRow(Guid AccountId, Guid ChordsId, string Kind, DateTimeOffset CreatedAt);

    private sealed class Entry
    {
        public Guid? SongId { get; private init; }

        public Guid? PlaylistId { get; private init; }

        public List<Guid> AlsoIn { get; } = [];

        public ChordsItem? BareChords { get; private init; }

        public required string Title { get; init; }

        public required IReadOnlyList<string> Artists { get; init; }

        public List<ChordsItem> Chords { get; } = [];

        public HashSet<Guid> People { get; } = [];

        public int Recordings { get; private set; }

        public double Position { get; private init; }

        public DateTimeOffset AddedAt { get; private set; }

        public DateTimeOffset ChordsAddedAt { get; private set; }

        public List<MarkRow> Marks { get; set; } = [];

        public string Text => Normalize(string.Join(' ', Chords.SelectMany(x => x.Artists.Append(x.Title ?? string.Empty)).Prepend(Title).Concat(Artists)));

        public string SortArtist => Normalize(string.Join(", ", Artists));

        public string SortTitle => Normalize(Title);

        public static Entry Of(SongData song, IEnumerable<Guid> people)
        {
            var entry = new Entry
            {
                SongId = song.Song.Id,
                PlaylistId = song.Song.PlaylistId,
                Title = song.Song.Title,
                Artists = song.Song.Artists,
                Position = song.Song.Position,
                AddedAt = song.Song.AddedAt,
                ChordsAddedAt = song.ChordsAddedAt,
                Recordings = song.Recordings,
            };
            entry.Chords.AddRange(song.Chords);
            entry.People.UnionWith(people);
            return entry;
        }

        public static Entry Bare(ChordsItem chords, DateTimeOffset at, Guid me)
        {
            var entry = new Entry
            {
                BareChords = chords,
                Title = chords.Title ?? string.Empty,
                Artists = chords.Artists,
                AddedAt = at,
                ChordsAddedAt = at,
            };
            entry.Chords.Add(chords);
            entry.People.Add(me);
            return entry;
        }

        public void Absorb(SongData song, IEnumerable<Guid> people)
        {
            if (!AlsoIn.Contains(song.Song.PlaylistId) && song.Song.PlaylistId != PlaylistId)
                AlsoIn.Add(song.Song.PlaylistId);
            Chords.AddRange(song.Chords.Where(x => Chords.All(c => c.Id != x.Id)));
            People.UnionWith(people);
            Recordings = Math.Max(Recordings, song.Recordings);
            ChordsAddedAt = ChordsAddedAt > song.ChordsAddedAt ? ChordsAddedAt : song.ChordsAddedAt;
        }

        public void Absorb(Entry other)
        {
            foreach (var playlist in other.AlsoIn.Append(other.PlaylistId!.Value).Where(x => x != PlaylistId && !AlsoIn.Contains(x)))
                AlsoIn.Add(playlist);
            Chords.AddRange(other.Chords.Where(x => Chords.All(c => c.Id != x.Id)));
            People.UnionWith(other.People);
            Recordings = Math.Max(Recordings, other.Recordings);
            ChordsAddedAt = ChordsAddedAt > other.ChordsAddedAt ? ChordsAddedAt : other.ChordsAddedAt;
        }

        /// <param name="chords">The chords as their sources give them to the caller.</param>
        /// <param name="covers">The songs' covers.</param>
        public M.SongRow Row(Guid? viewer, IReadOnlyDictionary<Guid, string> covers)
        {
            var others = Marks.Where(x => x.AccountId != viewer).ToList();
            return new()
            {
                SongId = SongId,
                PlaylistId = PlaylistId,
                AlsoIn = AlsoIn.Count == 0 ? null : AlsoIn,
                // chords alone, as the shelf keeps them
                Chords = BareChords == null ? null : ChordsReader.ToRef(BareChords, null),
                Title = Title,
                Artists = Artists,
                MyMarks = Marks.Where(x => x.AccountId == viewer).OrderByDescending(x => x.CreatedAt).Select(x => x.Kind).Distinct().ToList(),
                OtherMarks = others.GroupBy(x => x.Kind).OrderByDescending(x => x.Select(m => m.AccountId).Distinct().Count()).ThenBy(x => x.Min(m => m.CreatedAt))
                    .Take(OtherMarksShown).Select(x => x.Key).ToList(),
                OtherPeople = others.Select(x => x.AccountId).Distinct().Count(),
                ChordsCount = Chords.Count,
                ChordsIds = Chords.Select(x => x.SourceId).ToList(),
                Cover = SongId is { } song ? covers.GetValueOrDefault(song) : null,
                RecordingsCount = Recordings,
                AddedAt = AddedAt,
            };
        }
    }
}
