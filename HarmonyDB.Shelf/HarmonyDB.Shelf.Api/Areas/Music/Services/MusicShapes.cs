using System.Text.Json;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Model.Music;
using static HarmonyDB.Shelf.Api.Clients.Audio.AudioShapes;

namespace HarmonyDB.Shelf.Api.Areas.Music.Services;

/// <summary>
/// The music service's answers of both services in the one shape of the browser API. A track is made with nothing of
/// the person's on it; <see cref="TrackMarks"/> adds that.
/// </summary>
public static class MusicShapes
{
    public static MusicTrack Track(YTrack x) => new()
    {
        Service = MusicService.Yandex,
        Id = x.Id,
        Title = x.Title,
        Version = x.Version,
        Artists = Refs(x.Artists),
        Album = x.Album == null ? null : new() { Id = x.Album.Id, Title = x.Album.Title },
        DurationMs = x.DurationMs,
        CoverUrl = x.Cover,
        IsVideo = false,
        Available = x.Available,
        InMyPlaylists = false,
    };

    public static MusicTrack Track(TTrack x) => new()
    {
        Service = MusicService.Youtube,
        Id = x.Id!,
        Title = x.Title,
        Artists = Refs(x.Artists),
        Album = x.Album == null ? null : new() { Id = x.Album.Id, Title = x.Album.Title },
        DurationMs = x.DurationMs,
        CoverUrl = x.Cover,
        IsVideo = x.IsVideo,
        Available = x.Available,
        InMyPlaylists = false,
    };

    public static MusicTrack Track(TTrackPage x) => new()
    {
        Service = MusicService.Youtube,
        Id = x.Id,
        Title = x.Title,
        Artists = Refs(x.Artists),
        Album = x.Album == null ? null : new() { Id = x.Album.Id, Title = x.Album.Title },
        DurationMs = x.DurationMs,
        CoverUrl = x.Cover,
        IsVideo = x.IsVideo,
        InMyPlaylists = false,
    };

    /// <summary>
    /// A recording kept here, as it was described when it was kept.
    /// </summary>
    public static MusicTrack Track(Database.Model.Recording x) => new()
    {
        Service = FromDatabase(x.Service),
        Id = x.ServiceId,
        Title = x.Title,
        Artists = x.Artists.Select(a => new MusicArtistRef { Name = a }).ToList(),
        DurationMs = x.DurationSeconds == null ? null : (int)Math.Round(x.DurationSeconds.Value * 1000),
        CoverUrl = x.CoverUrl,
        InMyPlaylists = false,
    };

    /// <summary>
    /// The tracks that can be named: a YouTube entry without a video id is not a track.
    /// </summary>
    public static IReadOnlyList<MusicTrack> Tracks(IEnumerable<YTrack>? x) => (x ?? []).Select(Track).ToList();

    public static IReadOnlyList<MusicTrack> Tracks(IEnumerable<TTrack>? x) => (x ?? []).Where(t => !string.IsNullOrEmpty(t.Id)).Select(Track).ToList();

    public static MusicArtist Artist(YArtist x) => new() { Service = MusicService.Yandex, Id = x.Id, Name = x.Name, CoverUrl = x.Cover };

    public static IReadOnlyList<MusicArtist> Artists(IEnumerable<YArtist>? x) => (x ?? []).Select(Artist).ToList();

    public static IReadOnlyList<MusicArtist> Artists(IEnumerable<TArtist>? x)
        => (x ?? []).Where(a => !string.IsNullOrEmpty(a.Id))
            .Select(a => new MusicArtist { Service = MusicService.Youtube, Id = a.Id!, Name = a.Name, CoverUrl = a.Cover })
            .ToList();

    public static MusicAlbum Album(YAlbum x) => new()
    {
        Service = MusicService.Yandex,
        Id = x.Id,
        Title = string.IsNullOrEmpty(x.Version) ? x.Title : $"{x.Title} ({x.Version})",
        Type = x.Type,
        Year = x.Year,
        CoverUrl = x.Cover,
        TrackCount = x.TrackCount,
        Artists = Refs(x.Artists),
    };

    public static MusicAlbum Album(TAlbum x) => new()
    {
        Service = MusicService.Youtube,
        Id = x.Id!,
        Title = x.Title,
        Type = x.Type,
        Year = int.TryParse(x.Year, out var year) ? year : null,
        CoverUrl = x.Cover,
        TrackCount = x.TrackCount,
        Artists = Refs(x.Artists),
    };

    public static IReadOnlyList<MusicAlbum> Albums(IEnumerable<YAlbum>? x) => (x ?? []).Select(Album).ToList();

    public static IReadOnlyList<MusicAlbum> Albums(IEnumerable<TAlbum>? x) => (x ?? []).Where(a => !string.IsNullOrEmpty(a.Id)).Select(Album).ToList();

    public static MusicPlaylist Playlist(YPlaylist x) => new()
    {
        Service = MusicService.Yandex,
        Id = x.Id,
        Title = x.Title,
        CoverUrl = x.Cover,
        TrackCount = x.TrackCount,
        Owner = x.Owner,
    };

    public static MusicPlaylist Playlist(TPlaylist x) => new()
    {
        Service = MusicService.Youtube,
        Id = x.Id!,
        Title = x.Title,
        CoverUrl = x.Cover,
        TrackCount = Count(x.TrackCount),
        Owner = x.Author,
    };

    public static IReadOnlyList<MusicPlaylist> Playlists(IEnumerable<YPlaylist>? x) => (x ?? []).Select(Playlist).ToList();

    public static IReadOnlyList<MusicPlaylist> Playlists(IEnumerable<TPlaylist>? x) => (x ?? []).Where(p => !string.IsNullOrEmpty(p.Id)).Select(Playlist).ToList();

    public static MusicLyrics Lyrics(bool available, bool synced, IReadOnlyList<string>? writers, IEnumerable<LyricLine>? lines) => new()
    {
        Available = available,
        Synced = synced,
        Writers = writers,
        Lines = (lines ?? []).Select(l => new MusicLyricLine
        {
            StartMs = l.T,
            EndMs = l.End,
            Text = l.Text,
            Words = l.Words?.Select(w => new MusicLyricWord { StartMs = w.T, Text = w.Text }).ToList(),
        }).ToList(),
    };

    public static AudioServiceName ToAudio(MusicService service) => service switch
    {
        MusicService.Yandex => AudioServiceName.Yandex,
        MusicService.Youtube => AudioServiceName.Youtube,
        _ => throw new ArgumentOutOfRangeException(nameof(service)),
    };

    public static Database.Model.MusicService ToDatabase(MusicService service) => service switch
    {
        MusicService.Yandex => Database.Model.MusicService.Yandex,
        MusicService.Youtube => Database.Model.MusicService.Youtube,
        _ => throw new ArgumentOutOfRangeException(nameof(service)),
    };

    public static MusicService FromDatabase(Database.Model.MusicService service) => service switch
    {
        Database.Model.MusicService.Yandex => MusicService.Yandex,
        Database.Model.MusicService.Youtube => MusicService.Youtube,
        _ => throw new ArgumentOutOfRangeException(nameof(service)),
    };

    private static IReadOnlyList<MusicArtistRef> Refs(IEnumerable<YArtist>? x) => (x ?? []).Select(a => new MusicArtistRef { Id = a.Id, Name = a.Name }).ToList();

    private static IReadOnlyList<MusicArtistRef> Refs(IEnumerable<TArtist>? x) => (x ?? []).Select(a => new MusicArtistRef { Id = a.Id, Name = a.Name }).ToList();

    private static int? Count(JsonElement? value) => value switch
    {
        { ValueKind: JsonValueKind.Number } number when number.TryGetInt32(out var count) => count,
        { ValueKind: JsonValueKind.String } text when int.TryParse(new string(text.GetString()!.Where(char.IsDigit).ToArray()), out var count) => count,
        _ => null,
    };
}
