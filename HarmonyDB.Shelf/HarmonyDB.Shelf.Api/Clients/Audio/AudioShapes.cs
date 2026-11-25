using System.Text.Json;

namespace HarmonyDB.Shelf.Api.Clients.Audio;

/// <summary>
/// The music service's answers (the <c>data</c> of its envelope), with the fields read here; the rest is left out.
/// Yandex's shapes start with Y, YouTube's with T, as the service names them.
/// </summary>
public static class AudioShapes
{
    public sealed record Section<T>(int? Total, List<T>? Items);

    public sealed record LyricWord(int T, string Text);

    public sealed record LyricLine(int? T, int? End, string Text, List<LyricWord>? Words);

    // ---------------------------------------------------------------- Yandex

    public sealed record YArtist(string Id, string? Name, string? Cover, bool? Various);

    public sealed record YArtistDetail(string Id, string? Name, string? Cover, string? Description);

    public sealed record YAlbum(string Id, string? Title, string? Version, string? Type, int? Year, string? Cover, int? TrackCount, List<YArtist>? Artists);

    public sealed record YTrack(string Id, string? Title, string? Version, int? DurationMs, bool? Available, bool LyricsAvailable, string? Cover,
        List<YArtist>? Artists, YAlbum? Album, string? BackgroundVideo);

    public sealed record YPlaylist(string Id, string? Title, string? Description, int? TrackCount, string? Cover, string? Owner);

    public sealed record YSearch(Section<YTrack>? Tracks, Section<YArtist>? Artists, Section<YAlbum>? Albums);

    public sealed record YArtistPage(YArtistDetail Artist, List<YTrack>? PopularTracks, List<YAlbum>? Albums, List<YAlbum>? AlsoAlbums,
        List<YArtist>? SimilarArtists, List<YAlbum>? LastReleases);

    public sealed record YAlbums(List<YAlbum>? Albums);

    public sealed record YPagedTracks(List<YTrack>? Tracks);

    public sealed record YArtists(List<YArtist>? Artists);

    public sealed record YAlbumPage(YAlbum Album, List<YTrack>? Tracks);

    public sealed record YTracks(List<YTrack>? Tracks);

    public sealed record YLyrics(bool Available, List<string>? Writers, bool Synced, List<LyricLine>? Lines);

    public sealed record YPlaylistPage(YPlaylist Playlist, List<YTrack>? Tracks);

    public sealed record YChart(string? Title, List<YTrack>? Tracks);

    public sealed record YSubGenre(string? Id, string? Title);

    public sealed record YGenre(string? Id, string? Title, string? Image, string? Color, List<YSubGenre>? SubGenres);

    public sealed record YCategories(List<YGenre>? Genres);

    public sealed record YCategory(string? Title, List<YAlbum>? Albums);

    public sealed record YPlaylists(List<YPlaylist>? Playlists);

    public sealed record YLikes(int Total, List<YTrack>? Tracks);

    public sealed record YHistoryItem(List<YTrack>? Tracks);

    public sealed record YHistoryTab(string? Date, List<YHistoryItem>? Items);

    public sealed record YHistory(int Total, List<YHistoryTab>? Tabs);

    // ---------------------------------------------------------------- YouTube

    public sealed record TArtist(string? Id, string? Name, string? Cover);

    public sealed record TAlbum(string? Id, string? Title, string? Year, string? Type, int? TrackCount, string? Cover, List<TArtist>? Artists);

    public sealed record TTrack(string? Id, string? Title, int? DurationMs, string? Cover, List<TArtist>? Artists, TAlbum? Album, bool? IsVideo, bool? Available);

    /// <param name="TrackCount">A number, or the service's text of it.</param>
    public sealed record TPlaylist(string? Id, string? Title, string? Cover, JsonElement? TrackCount, string? Author);

    public sealed record TSearch(Section<TTrack>? Tracks, Section<TTrack>? Videos, Section<TArtist>? Artists, Section<TAlbum>? Albums);

    public sealed record TArtistDetail(string Id, string? Name, string? Cover, string? Description);

    public sealed record TArtistPage(TArtistDetail Artist, List<TTrack>? Songs, List<TAlbum>? Albums, List<TAlbum>? Singles, List<TTrack>? Videos, List<TArtist>? Related);

    public sealed record TAlbums(List<TAlbum>? Albums);

    public sealed record TAlbumPage(TAlbum Album, List<TTrack>? Tracks);

    public sealed record TPlaylistPage(TPlaylist Playlist, List<TTrack>? Tracks);

    public sealed record TTrackPage(string Id, string? Title, List<TArtist>? Artists, TAlbum? Album, int? DurationMs, string? Cover, bool? IsVideo, bool LyricsAvailable);

    public sealed record TTracks(List<TTrack>? Tracks);

    public sealed record TSimilarSection(string? Title, List<TTrack>? Tracks, List<TAlbum>? Albums, List<TArtist>? Artists);

    public sealed record TSimilar(List<TSimilarSection>? Sections);

    public sealed record TLyrics(bool Available, bool Synced, List<LyricLine>? Lines);

    public sealed record TChart(List<TTrack>? Songs, List<TTrack>? Videos, List<TArtist>? Artists, List<TTrack>? Trending);

    public sealed record TCategoryItem(string Id, string? Title, string? Color);

    public sealed record TCategorySection(string? Title, List<TCategoryItem>? Items);

    public sealed record TCategories(List<TCategorySection>? Sections);

    public sealed record TCategoryShelf(string? Title, List<TPlaylist>? Playlists, List<TAlbum>? Albums, List<TTrack>? Tracks, List<TArtist>? Artists);

    public sealed record TCategory(List<TCategoryShelf>? Sections);

    public sealed record TPlaylists(List<TPlaylist>? Playlists);

    public sealed record TLikes(int Total, List<TTrack>? Tracks);
}
