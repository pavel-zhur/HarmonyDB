using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Shelf.Model.Music;
using HarmonyDB.Shelf.Model.Playlists;
using HarmonyDB.Source.Api.Model.V2;

namespace HarmonyDB.Shelf.Api.Areas.Mcp.Tools;

/// <summary>
/// The rows of the tools' answers: what an assistant needs to choose a thing and to call the next tool (its id, title,
/// artists, a word of what it is), and no more — every answer goes through the assistant's context, so it stays within
/// a few kilobytes: lists come a small page at a time, big things by a link.
/// </summary>
public static class Slim
{
    public const int DefaultPage = 10;
    public const int MaxPage = 25;

    public sealed record Page<T>(int Total, int Offset, IReadOnlyList<T> Items);

    public static (int Offset, int Limit) Paging(int? offset, int? limit, int defaultLimit = DefaultPage)
        => offset is < 0 || limit is < 1 or > MaxPage ? throw ShelfException.BadRequest() : (offset ?? 0, limit ?? defaultLimit);

    public static Page<TOut> Paged<TIn, TOut>(IReadOnlyCollection<TIn> all, int? offset, int? limit, Func<TIn, TOut> map, int defaultLimit = DefaultPage)
    {
        var (skip, take) = Paging(offset, limit, defaultLimit);
        return new(all.Count, skip, all.Skip(skip).Take(take).Select(map).ToList());
    }

    public static string? Short(string? text, int length) => text == null || text.Length <= length ? text : text[..length] + "…";

    public static object Chords(ChordsHeader x) => new
    {
        id = x.Id,
        x.Title,
        artists = string.Join(", ", x.Artists),
        source = x.Source.Title,
        key = x.Attributes.Key,
        kinds = x.Kinds.Count == 1 && x.Kinds[0] == ChordsKind.Sheet ? null : x.Kinds,
    };

    public static object Chords(ChordsAnswer x) => x.Header is { } header && x.State == ChordsState.Open ? Chords(header) : new { id = x.Id, state = x.State };

    public static object Playlist(PlaylistSummary x) => new
    {
        x.Id,
        x.Title,
        x.Access,
        role = x.MyRole,
        kind = x.SystemKind == PlaylistSystemKind.None ? (PlaylistSystemKind?)null : x.SystemKind,
        songs = x.SongCount,
        members = x.MemberCount,
    };

    public static object Playlist(PlaylistDetails x) => new
    {
        playlist = Playlist(x.Playlist),
        description = Short(x.Playlist.Description, 300),
        whoMayAdd = x.Playlist.AddPolicy,
        owner = x.Playlist.Owner.Name,
        x.CanAdd,
        x.CanAdminister,
        membersShown = Math.Min(x.Members.Count, 30),
        members = x.Members.Take(30).Select(m => new { accountId = m.Person.Id, name = m.Person.Name, m.Role }).ToList(),
        invitations = x.Invitations.Where(i => i.RevokedAt == null).Take(10).Select(i => new { i.Token, i.Role, i.ExpiresAt, i.Uses, i.MaxUses }).ToList(),
    };

    /// <summary>
    /// A page of songs, each with the chords of the person's main variant (of chords alone: those chords).
    /// </summary>
    public static async Task<Page<object>> SongsAsync(ClaimsPrincipal user, Views views, SongList list, CancellationToken cancellationToken)
    {
        var main = await views.MainChordsAsync(user.GetAccountId(), list.Rows.Where(x => x.SongId != null).Select(x => x.SongId!.Value).ToList(), cancellationToken);
        return new(list.Total, list.Offset, list.Rows.Select(x => Song(x, x.SongId is { } song ? main.GetValueOrDefault(song) : x.Chords?.Id)).ToList());
    }

    public static object Song(SongRow x, string? chordsId) => new
    {
        songId = x.SongId,
        playlistId = x.PlaylistId,
        x.Title,
        artists = string.Join(", ", x.Artists),
        chordsId,
        myMarks = x.MyMarks.Count > 0 ? x.MyMarks : null,
        othersMarked = x.OtherPeople > 0 ? x.OtherPeople : (int?)null,
        variants = x.ChordsCount,
        recordings = x.RecordingsCount,
    };

    public static object Track(MusicTrack x, bool showLibrary) => new
    {
        x.Id,
        x.Title,
        artists = string.Join(", ", x.Artists.Select(a => a.Name)),
        album = x.Album?.Title,
        seconds = x.DurationMs / 1000,
        video = x.IsVideo == true ? true : (bool?)null,
        inMyPlaylists = showLibrary && x.InMySongs.Count > 0 ? true : (bool?)null,
    };

    public static object Album(MusicAlbum x) => new { x.Id, x.Title, artists = string.Join(", ", x.Artists.Select(a => a.Name)), x.Year, tracks = x.TrackCount };

    public static object Artist(MusicArtist x) => new { x.Id, x.Name };

    public static object MusicPlaylist(Model.Music.MusicPlaylist x) => new { x.Id, x.Title, x.Owner, tracks = x.TrackCount };

    /// <summary>
    /// What a kind of content of an item holds, in a few numbers — the data itself goes by a link.
    /// </summary>
    public static object Summary(Content content) => new
    {
        timeline = content.Timeline is { } t
            ? new
            {
                seconds = Math.Round(t.Duration),
                keys = t.Keys.Select(k => k.Key).Where(k => k != null).Distinct().ToList(),
                tempos = t.Bars.Where(b => b.Tempo != null).Select(b => Math.Round(b.Tempo!.Bpm)).Distinct().Take(5).ToList(),
                timeSignatures = t.Bars.Select(b => b.TimeSignature).Distinct().ToList(),
                bars = t.Bars.Count,
                chords = t.Chords.Count,
                sections = t.Sections.Count,
            }
            : null,
        lyrics = content.Lyrics is { } l ? new { l.Language, lines = l.Lines.Count, firstLine = Short(l.Lines.FirstOrDefault()?.Text, 80) } : null,
        notes = content.Notes is { } n ? new { parts = n.Parts.Select(p => new { p.Name, notes = p.Notes.Count }).ToList(), drumHits = n.Drums.Count, midi = n.Midi != null } : null,
        score = content.Score is { } s ? new { musicXmlCharacters = s.MusicXml.Length } : null,
        chart = content.Chart is { } c ? new { bandChartParts = c.BandChart?.Parts.Count, iReal = c.IReal != null } : null,
        recording = content.Recording is { } r ? new { seconds = Math.Round(r.Duration), stems = r.Stems.Select(x => x.Stem).ToList() } : null,
    };
}
