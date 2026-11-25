using System.Security.Claims;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Model.Music;
using static HarmonyDB.Shelf.Api.Clients.Audio.AudioShapes;

namespace HarmonyDB.Shelf.Api.Areas.Music.Services;

/// <summary>
/// The navigation of both music services, in the browser API's one shape, every track marked for the person. It is
/// asked anonymously — playlists by id too —, except the few things a service gives to an account only and that are not
/// about the account (<see cref="YandexAccountOnly"/>, <see cref="RecommendAsync"/>). The service account is never used to
/// read lists that could be its own.
/// </summary>
public sealed class MusicCatalog(AudioMusicClient audio, TrackMarks marks)
{
    public async Task<MusicArtistPage> GetArtistAsync(ClaimsPrincipal user, MusicService service, string id, CancellationToken cancellationToken)
    {
        if (service == MusicService.Yandex)
        {
            var x = await GetAsync<YArtistPage>(service, $"artists/{E(id)}", cancellationToken);
            return await marks.ApplyAsync(user, m => new MusicArtistPage
            {
                Artist = new() { Service = service, Id = x.Artist.Id, Name = x.Artist.Name, CoverUrl = x.Artist.Cover },
                Description = x.Artist.Description,
                PopularTracks = MusicShapes.Tracks(x.PopularTracks).Select(m).ToList(),
                Albums = MusicShapes.Albums(x.Albums),
                OtherReleases = MusicShapes.Albums(x.AlsoAlbums),
                Videos = [],
                SimilarArtists = MusicShapes.Artists(x.SimilarArtists),
            }, cancellationToken);
        }

        var t = await GetAsync<TArtistPage>(service, $"artists/{E(id)}", cancellationToken);
        return await marks.ApplyAsync(user, m => new MusicArtistPage
        {
            Artist = new() { Service = service, Id = t.Artist.Id, Name = t.Artist.Name, CoverUrl = t.Artist.Cover },
            Description = t.Artist.Description,
            PopularTracks = MusicShapes.Tracks(t.Songs).Select(m).ToList(),
            Albums = MusicShapes.Albums(t.Albums),
            OtherReleases = MusicShapes.Albums(t.Singles),
            Videos = MusicShapes.Tracks(t.Videos).Select(m).ToList(),
            SimilarArtists = MusicShapes.Artists(t.Related),
        }, cancellationToken);
    }

    public async Task<MusicAlbumList> GetArtistAlbumsAsync(MusicService service, string id, int page, CancellationToken cancellationToken)
        => service == MusicService.Yandex
            ? new() { Albums = MusicShapes.Albums((await GetAsync<YAlbums>(service, $"artists/{E(id)}/albums?page={page}", cancellationToken)).Albums) }
            : new() { Albums = MusicShapes.Albums((await GetAsync<TAlbums>(service, $"artists/{E(id)}/albums", cancellationToken)).Albums) };

    /// <summary>
    /// All of an artist's tracks, page by page: Yandex only (YouTube's artist page has its songs).
    /// </summary>
    public async Task<MusicTrackList> GetArtistTracksAsync(ClaimsPrincipal user, MusicService service, string id, int page, CancellationToken cancellationToken)
    {
        if (service != MusicService.Yandex)
            throw ShelfException.BadRequest(MusicErrorCodes.NotOnThisService);

        var x = await GetAsync<YPagedTracks>(service, $"artists/{E(id)}/tracks?page={page}", cancellationToken);
        return await marks.ApplyAsync(user, m => new MusicTrackList { Tracks = MusicShapes.Tracks(x.Tracks).Select(m).ToList() }, cancellationToken);
    }

    public async Task<MusicArtistList> GetSimilarArtistsAsync(MusicService service, string id, CancellationToken cancellationToken)
        => service == MusicService.Yandex
            ? new() { Artists = MusicShapes.Artists((await GetAsync<YArtists>(service, $"artists/{E(id)}/similar", cancellationToken)).Artists) }
            : new() { Artists = MusicShapes.Artists((await GetAsync<TArtistPage>(service, $"artists/{E(id)}", cancellationToken)).Related) };

    /// <summary>
    /// A radio by the artist: seeded by the artist's most popular track.
    /// </summary>
    public async Task<MusicTrackList> GetArtistRadioAsync(ClaimsPrincipal user, MusicService service, string id, CancellationToken cancellationToken)
    {
        var page = await GetArtistAsync(user, service, id, cancellationToken);
        var seed = page.PopularTracks.FirstOrDefault();
        return seed == null
            ? new() { Title = page.Artist.Name, Tracks = [] }
            : (await GetRadioAsync(user, service, seed.Id, cancellationToken)) with { Title = page.Artist.Name };
    }

    public async Task<MusicAlbumPage> GetAlbumAsync(ClaimsPrincipal user, MusicService service, string id, CancellationToken cancellationToken)
    {
        if (service == MusicService.Yandex)
        {
            var x = await GetAsync<YAlbumPage>(service, $"albums/{E(id)}", cancellationToken);
            return await marks.ApplyAsync(user, m => new MusicAlbumPage { Album = MusicShapes.Album(x.Album), Tracks = MusicShapes.Tracks(x.Tracks).Select(m).ToList() }, cancellationToken);
        }

        var t = await GetAsync<TAlbumPage>(service, $"albums/{E(id)}", cancellationToken);
        return await marks.ApplyAsync(user, m => new MusicAlbumPage
        {
            Album = MusicShapes.Album(t.Album with { Id = t.Album.Id ?? id }),
            Tracks = MusicShapes.Tracks(t.Tracks).Select(m).ToList(),
        }, cancellationToken);
    }

    public async Task<MusicTrackPage> GetTrackAsync(ClaimsPrincipal user, MusicService service, string id, CancellationToken cancellationToken)
    {
        if (service == MusicService.Yandex)
        {
            var x = await GetAsync<YTrack>(service, $"tracks/{E(id)}", cancellationToken);
            return await marks.ApplyAsync(user, m => new MusicTrackPage { Track = m(MusicShapes.Track(x)), LyricsAvailable = x.LyricsAvailable }, cancellationToken);
        }

        var t = await GetAsync<TTrackPage>(service, $"tracks/{E(id)}", cancellationToken);
        return await marks.ApplyAsync(user, m => new MusicTrackPage { Track = m(MusicShapes.Track(t)), LyricsAvailable = t.LyricsAvailable }, cancellationToken);
    }

    public async Task<MusicSimilar> GetSimilarAsync(ClaimsPrincipal user, MusicService service, string id, CancellationToken cancellationToken)
    {
        if (service == MusicService.Yandex)
        {
            var x = await GetAsync<YTracks>(service, $"tracks/{E(id)}/similar", cancellationToken);
            return await marks.ApplyAsync(user, m => new MusicSimilar { Tracks = MusicShapes.Tracks(x.Tracks).Select(m).ToList(), Albums = [], Artists = [] }, cancellationToken);
        }

        var t = await GetAsync<TSimilar>(service, $"tracks/{E(id)}/similar", cancellationToken);
        var sections = t.Sections ?? [];
        return await marks.ApplyAsync(user, m => new MusicSimilar
        {
            Tracks = MusicShapes.Tracks(sections.SelectMany(s => s.Tracks ?? [])).DistinctBy(x => x.Id).Select(m).ToList(),
            Albums = MusicShapes.Albums(sections.SelectMany(s => s.Albums ?? [])).DistinctBy(x => x.Id).ToList(),
            Artists = MusicShapes.Artists(sections.SelectMany(s => s.Artists ?? [])).DistinctBy(x => x.Id).ToList(),
        }, cancellationToken);
    }

    public async Task<MusicTrackList> GetRadioAsync(ClaimsPrincipal user, MusicService service, string id, CancellationToken cancellationToken)
        => await Tracks(user, await GetRadioTracksAsync(service, id, cancellationToken), cancellationToken);

    /// <summary>
    /// A radio seeded by the track, its tracks not marked for anyone.
    /// </summary>
    public async Task<IReadOnlyList<MusicTrack>> GetRadioTracksAsync(MusicService service, string id, CancellationToken cancellationToken)
        => service == MusicService.Yandex
            ? MusicShapes.Tracks((await GetAsync<YTracks>(service, $"tracks/{E(id)}/radio", cancellationToken, YandexAccountOnly)).Tracks)
            : MusicShapes.Tracks((await GetAsync<TTracks>(service, $"tracks/{E(id)}/radio", cancellationToken)).Tracks);

    public async Task<MusicLyrics> GetLyricsAsync(MusicService service, string id, CancellationToken cancellationToken)
    {
        if (service == MusicService.Yandex)
        {
            var x = await GetAsync<YLyrics>(service, $"tracks/{E(id)}/lyrics", cancellationToken, YandexAccountOnly);
            return MusicShapes.Lyrics(x.Available, x.Synced, x.Writers, x.Lines);
        }

        var t = await GetAsync<TLyrics>(service, $"tracks/{E(id)}/lyrics", cancellationToken);
        return MusicShapes.Lyrics(t.Available, t.Synced, null, t.Lines);
    }

    public async Task<MusicPlaylistPage> GetPlaylistAsync(ClaimsPrincipal user, MusicService service, string id, AudioActing acting, CancellationToken cancellationToken)
    {
        if (service == MusicService.Yandex)
        {
            var x = await GetAsync<YPlaylistPage>(service, $"playlists/{E(id)}", cancellationToken, acting);
            return await marks.ApplyAsync(user, m => new MusicPlaylistPage { Playlist = MusicShapes.Playlist(x.Playlist), Tracks = MusicShapes.Tracks(x.Tracks).Select(m).ToList() }, cancellationToken);
        }

        var t = await GetAsync<TPlaylistPage>(service, $"playlists/{E(id)}", cancellationToken, acting);
        return await marks.ApplyAsync(user, m => new MusicPlaylistPage
        {
            Playlist = MusicShapes.Playlist(t.Playlist with { Id = t.Playlist.Id ?? id }),
            Tracks = MusicShapes.Tracks(t.Tracks).Select(m).ToList(),
        }, cancellationToken);
    }

    /// <param name="country">YouTube: the country's code, <c>ZZ</c> (the world) when none.</param>
    public async Task<MusicChart> GetChartAsync(ClaimsPrincipal user, MusicService service, string? country, CancellationToken cancellationToken)
    {
        if (service == MusicService.Yandex)
        {
            var x = await GetAsync<YChart>(service, "chart", cancellationToken);
            return await marks.ApplyAsync(user, m => new MusicChart { Tracks = MusicShapes.Tracks(x.Tracks).Select(m).ToList(), Artists = [] }, cancellationToken);
        }

        var t = await GetAsync<TChart>(service, $"chart?country={E(string.IsNullOrWhiteSpace(country) ? "ZZ" : country)}", cancellationToken);
        return await marks.ApplyAsync(user, m => new MusicChart
        {
            Tracks = MusicShapes.Tracks((t.Songs ?? []).Concat(t.Videos ?? []).Concat(t.Trending ?? [])).DistinctBy(x => x.Id).Select(m).ToList(),
            Artists = MusicShapes.Artists(t.Artists),
        }, cancellationToken);
    }

    /// <param name="language">The language the names are given in (<see cref="MusicLanguages"/>).</param>
    public async Task<MusicCategories> GetCategoriesAsync(MusicService service, string language, CancellationToken cancellationToken)
    {
        if (service == MusicService.Yandex)
        {
            var x = await GetAsync<YCategories>(service, $"categories?lang={E(language)}", cancellationToken);
            return new()
            {
                Groups =
                [
                    new()
                    {
                        Categories = (x.Genres ?? []).Where(g => !string.IsNullOrEmpty(g.Id)).Select(g => new MusicCategory
                        {
                            Id = g.Id!,
                            Title = g.Title,
                            Color = g.Color,
                            ImageUrl = g.Image,
                            Children = (g.SubGenres ?? []).Where(s => !string.IsNullOrEmpty(s.Id))
                                .Select(s => new MusicCategory { Id = s.Id!, Title = s.Title, Children = [] }).ToList(),
                        }).ToList(),
                    },
                ],
            };
        }

        var t = await GetAsync<TCategories>(service, $"categories?lang={E(language)}", cancellationToken);
        return new()
        {
            Groups = (t.Sections ?? []).Select(s => new MusicCategoryGroup
            {
                Title = s.Title,
                Categories = (s.Items ?? []).Select(i => new MusicCategory { Id = i.Id, Title = i.Title, Color = i.Color, Children = [] }).ToList(),
            }).ToList(),
        };
    }

    /// <param name="offset">Yandex: how many of the genre's albums to skip.</param>
    /// <param name="language">The language the page's names are given in (<see cref="MusicLanguages"/>).</param>
    public async Task<MusicCategoryPage> GetCategoryAsync(ClaimsPrincipal user, MusicService service, string id, int offset, string language, CancellationToken cancellationToken)
    {
        if (service == MusicService.Yandex)
        {
            var x = await GetAsync<YCategory>(service, $"categories/{E(id)}?offset={offset}&lang={E(language)}", cancellationToken);
            return new() { Title = x.Title, Albums = MusicShapes.Albums(x.Albums), Playlists = [], Tracks = [], Artists = [] };
        }

        var t = await GetAsync<TCategory>(service, $"categories/{E(id)}?lang={E(language)}", cancellationToken);
        var sections = t.Sections ?? [];
        return await marks.ApplyAsync(user, m => new MusicCategoryPage
        {
            Albums = MusicShapes.Albums(sections.SelectMany(s => s.Albums ?? [])).DistinctBy(x => x.Id).ToList(),
            Playlists = MusicShapes.Playlists(sections.SelectMany(s => s.Playlists ?? [])).DistinctBy(x => x.Id).ToList(),
            Tracks = MusicShapes.Tracks(sections.SelectMany(s => s.Tracks ?? [])).DistinctBy(x => x.Id).Select(m).ToList(),
            Artists = MusicShapes.Artists(sections.SelectMany(s => s.Artists ?? [])).DistinctBy(x => x.Id).ToList(),
        }, cancellationToken);
    }

    /// <summary>
    /// A search of one service: its tracks and its artists.
    /// </summary>
    public async Task<(IReadOnlyList<MusicTrack> Tracks, IReadOnlyList<MusicArtist> Artists)> SearchAsync(MusicService service, string text, CancellationToken cancellationToken)
    {
        if (service == MusicService.Yandex)
        {
            var x = await GetAsync<YSearch>(service, $"search?text={E(text)}", cancellationToken);
            return (MusicShapes.Tracks(x.Tracks?.Items), MusicShapes.Artists(x.Artists?.Items));
        }

        var t = await GetAsync<TSearch>(service, $"search?text={E(text)}", cancellationToken);
        return (MusicShapes.Tracks((t.Tracks?.Items ?? []).Concat(t.Videos?.Items ?? [])).DistinctBy(x => x.Id).ToList(), MusicShapes.Artists(t.Artists?.Items));
    }

    /// <summary>
    /// Tracks the service recommends for a set of its tracks: Yandex only. The music service does it by the service
    /// account (a temporary playlist of these tracks is made in it, read and deleted); the answer is about the tracks
    /// given, not the account's own lists.
    /// </summary>
    public async Task<IReadOnlyList<MusicTrack>> RecommendAsync(IReadOnlyList<string> yandexTrackIds, CancellationToken cancellationToken)
        => MusicShapes.Tracks((await Call(() => audio.PostAsync<YTracks>("yandex/recommendations",
            new { tracks = yandexTrackIds.Select(x => new { id = x }).ToList() }, AudioActing.Service, cancellationToken))).Tracks);

    public Task<MusicTrackList> Tracks(ClaimsPrincipal user, IReadOnlyList<MusicTrack> tracks, CancellationToken cancellationToken)
        => marks.ApplyAsync(user, m => new MusicTrackList { Tracks = tracks.Select(m).ToList() }, cancellationToken);

    public Task<T> GetAsync<T>(MusicService service, string path, CancellationToken cancellationToken, AudioActing? acting = null)
        => Call(() => audio.GetAsync<T>($"{AudioMusicClient.Segment(MusicShapes.ToAudio(service))}/{path}", acting ?? AudioActing.Anonymous, cancellationToken));

    /// <summary>
    /// A refusal of the music service as the browser API's error: what it does not have is not found, anything else is
    /// "unavailable".
    /// </summary>
    public static async Task<T> Call<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (AudioException exception) when (exception.StatusCode == StatusCodes.Status404NotFound)
        {
            throw ShelfException.NotFound();
        }
        catch (AudioException exception) when (exception.Code is "credentials_rejected" or "credentials_invalid")
        {
            throw ShelfException.Conflict(MusicErrorCodes.MusicLinkRejected);
        }
        catch (AudioException)
        {
            throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
        }
    }

    private static string E(string value) => Uri.EscapeDataString(value);

    /// <summary>
    /// Yandex gives a track's lyrics and a radio seeded by a track to an account only: these are asked as the service
    /// account. Both answers are about the track (its words; tracks like it), not about the account's own lists.
    /// </summary>
    private static AudioActing YandexAccountOnly => AudioActing.Service;
}
