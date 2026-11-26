using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using HarmonyDB.Shelf.Api.Areas.App.Services;
using HarmonyDB.Shelf.Api.Areas.Illustrations.Services;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Areas.Share.Services;
using HarmonyDB.Shelf.Api.Clients.Illustrations;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Shelf.Model.Playlists;
using HarmonyDB.Shelf.Model.Share;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.PublicPage.Services;

/// <summary>
/// What a messenger shows of an address of the public page: the head's title, description and picture for it, written
/// as HTML that the page's server puts into the page in place of its own (its nginx, by an include). Only what a guest
/// who holds the address sees anyway, read as a guest (never as whoever asks):
/// <list type="bullet">
/// <item><c>/</c>: the showcase playlist (<see cref="AppOptions.ShowcasePlaylistId"/>), while it is open to guests;</item>
/// <item><c>/songs/{songId}</c>: a song of the showcase;</item>
/// <item><c>/chords/{id}</c>: chords open to a guest;</item>
/// <item><c>/s/{token}</c>: what the link opens to a guest (a link that carries its sharer's access: the chords as the
/// sharer's access opens them), and <c>/s/{token}/{songId}</c>: a song opened by the link.</item>
/// </list>
/// Anything else, a link that does not open, or what is not open to a guest: nothing (the page keeps its own head, so
/// nothing tells that something is there). The picture: one of the song's or chords' illustrations, chosen by the
/// address (the same one every time), else its recording's cover, else the page's own picture. The preview of an
/// address that is not a link is kept a few minutes for every reader of it (in <see cref="PreviewCache"/>, bounded); a
/// link's is read each time, so that a link removed shows nothing at once; a path that is no address is not kept.
/// </summary>
public sealed class PagePreviews(
    ShelfDbContext db,
    PlaylistGate gate,
    SongPages songPages,
    ShareService shares,
    Views views,
    IllustrationsClient illustrations,
    PreviewCache cache,
    IOptionsMonitor<PublicPageOptions> options,
    IOptionsMonitor<AppOptions> app)
{
    /// <summary>
    /// The longest address written for; a longer one is no address of the page.
    /// </summary>
    public const int MaxPathLength = 256;

    /// <summary>
    /// The most variants of a song whose illustrations are looked at.
    /// </summary>
    private const int MaxVariants = 5;

    private static readonly ClaimsPrincipal Guest = new(new ClaimsIdentity());

    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    /// <summary>
    /// The head's tags for the address (its path, without the query); null when nothing is written for it.
    /// </summary>
    public async Task<string?> GetAsync(string path, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        if (path.Length > MaxPathLength)
            return null;

        // kept: the addresses of the page that are not links; anything else is not kept at all
        var keep = path.Split('/', StringSplitOptions.RemoveEmptyEntries) switch
        {
            [] => true,
            ["songs", var song] => Guid.TryParse(song, out _),
            ["chords", _] => true,
            _ => false,
        };
        if (keep && cache.TryGet(path, out var kept))
            return kept;

        Preview? preview;
        try
        {
            preview = await ReadAsync(path, cancellationToken);
        }
        catch (ShelfException exception) when (exception.StatusCode == StatusCodes.Status503ServiceUnavailable)
        {
            // a source or a service did not answer: nothing written, and nothing kept
            return null;
        }

        var html = preview == null ? null : Write(path, preview, settings);
        if (keep)
            cache.Set(path, html, settings.Keep);
        return html;
    }

    private async Task<Preview?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        try
        {
            return parts switch
            {
                [] => await ShowcaseAsync(cancellationToken),
                ["songs", var song] when Guid.TryParse(song, out var songId) => await ShowcaseSongAsync(path, songId, cancellationToken),
                ["chords", var id] => await ChordsAsync(path, id, cancellationToken),
                ["s", var token] => await LinkAsync(path, token, cancellationToken),
                ["s", var token, var song] when Guid.TryParse(song, out var songId) => await LinkedSongAsync(path, token, songId, cancellationToken),
                _ => null,
            };
        }
        catch (ShelfException exception) when (exception.StatusCode is >= 400 and < 500)
        {
            // not there, or not open to a guest: as if nothing were
            return null;
        }
    }

    private async Task<Preview?> ShowcaseAsync(CancellationToken cancellationToken)
    {
        if (app.CurrentValue.ShowcasePlaylist is not { } playlistId)
            return null;

        var view = await gate.ReadAsync(null, playlistId, null, cancellationToken);
        return Playlist(view.Playlist.Title, view.Playlist.Description);
    }

    private async Task<Preview?> ShowcaseSongAsync(string path, Guid songId, CancellationToken cancellationToken)
        => app.CurrentValue.ShowcasePlaylist is { } playlistId
            ? await SongAsync(path, await songPages.GetAsync(Guest, playlistId, songId, null, cancellationToken), cancellationToken)
            : null;

    private async Task<Preview?> ChordsAsync(string path, string id, CancellationToken cancellationToken)
    {
        var answer = (await views.ChordsAsync(Guest, [id], cancellationToken))[id];
        return await OpenChordsAsync(path, answer, cancellationToken);
    }

    private async Task<Preview?> LinkAsync(string path, string token, CancellationToken cancellationToken)
    {
        var view = await shares.OpenAsync(Guest, token, cancellationToken);
        return view.Kind switch
        {
            ShareKind.Playlist => Playlist(view.Playlist!.Title, view.Playlist.Description),
            ShareKind.Song => await SongAsync(path, view.Song!, cancellationToken),
            _ => await OpenChordsAsync(path, view.Chords!, cancellationToken),
        };
    }

    private async Task<Preview?> LinkedSongAsync(string path, string token, Guid songId, CancellationToken cancellationToken)
    {
        var playlistId = await db.PlaylistSongs.Where(x => x.Id == songId).Select(x => (Guid?)x.PlaylistId).SingleOrDefaultAsync(cancellationToken);
        return playlistId == null ? null : await SongAsync(path, await songPages.GetAsync(Guest, playlistId.Value, songId, token, cancellationToken), cancellationToken);
    }

    private async Task<Preview?> OpenChordsAsync(string path, ChordsAnswer answer, CancellationToken cancellationToken)
    {
        if (answer.State != ChordsState.Open || answer.Header == null)
            return null;

        return Song(answer.Header.Title ?? answer.Id, answer.Header.Artists, await IllustrationAsync(path, [answer.Id], cancellationToken));
    }

    private async Task<Preview> SongAsync(string path, SongPage page, CancellationToken cancellationToken)
    {
        var open = page.Variants.Where(x => x.Chords.State == ChordsState.Open).Select(x => x.Chords.Id).Distinct(StringComparer.Ordinal).Take(MaxVariants).ToList();
        var picture = await IllustrationAsync(path, open, cancellationToken)
                      ?? (page.Recordings.Select(x => x.Recording.CoverUrl).FirstOrDefault(x => x != null) is { } cover ? new Picture(cover, null) : null);
        return Song(page.Song.Title, page.Song.Artists, picture);
    }

    /// <summary>
    /// One of the chords' illustrations, chosen by the address: the same one for it every time. None when they have
    /// none, or the illustrations do not answer.
    /// </summary>
    private async Task<Picture?> IllustrationAsync(string path, IReadOnlyList<string> chordsIds, CancellationToken cancellationToken)
    {
        if (chordsIds.Count == 0)
            return null;

        try
        {
            var images = new List<Guid>();
            foreach (var id in chordsIds)
                images.AddRange((await illustrations.ReadAsync(id, cancellationToken)).Select(x => x.Id));
            if (images.Count == 0)
                return null;

            var size = (await illustrations.GetPresetsAsync(cancellationToken)).Sizes.Max();
            var chosen = Choose(path, images.Distinct().Order().ToList());
            return new($"{IllustrationsService.ImagesPath}/{chosen}/{size}", size);
        }
        catch (ShelfException exception) when (exception.StatusCode == StatusCodes.Status503ServiceUnavailable)
        {
            return null;
        }
    }

    /// <summary>
    /// One of the items, by the address: the same for the same address and items.
    /// </summary>
    public static T Choose<T>(string path, IReadOnlyList<T> items)
        => items[(int)(BitConverter.ToUInt32(SHA256.HashData(Encoding.UTF8.GetBytes(path)), 0) % (uint)items.Count)];

    /// <summary>
    /// A playlist: its title, and its description under it.
    /// </summary>
    private static Preview Playlist(string title, string? description)
        => new(title, title, string.IsNullOrWhiteSpace(description) ? null : description, null);

    /// <summary>
    /// A song or chords: the title, with the artists under it and after it in the page's title.
    /// </summary>
    private static Preview Song(string title, IReadOnlyList<string> artists, Picture? picture)
    {
        var line = artists.Count == 0 ? null : string.Join(", ", artists);
        return new(line == null ? title : $"{title} — {line}", title, line, picture);
    }

    private static string Write(string path, Preview preview, PublicPageOptions settings)
    {
        string Absolute(string address) => address.StartsWith('/') ? settings.Origin + address : address;

        var (image, size) = preview.Picture is { } picture
            ? (Absolute(picture.Address), picture.Size)
            : (Absolute(settings.DefaultImagePath), (int?)settings.DefaultImageSize);
        var html = new StringBuilder();
        html.Append("<title>").Append(Encoder.Encode(preview.PageTitle)).Append("</title>\n");
        Meta(html, "name", "description", preview.Description);
        Meta(html, "property", "og:url", settings.Origin + (path.Length == 0 ? "/" : path));
        Meta(html, "property", "og:title", preview.Title);
        Meta(html, "property", "og:description", preview.Description);
        Meta(html, "property", "og:image", image);
        if (size != null)
        {
            Meta(html, "property", "og:image:width", size.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Meta(html, "property", "og:image:height", size.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        Meta(html, "name", "twitter:card", "summary");
        return html.ToString();
    }

    private static void Meta(StringBuilder html, string attribute, string name, string? content)
    {
        if (content != null)
            html.Append("<meta ").Append(attribute).Append("=\"").Append(name).Append("\" content=\"").Append(Encoder.Encode(content)).Append("\">\n");
    }

    /// <param name="PageTitle">The page's title.</param>
    /// <param name="Title">The preview's title.</param>
    /// <param name="Description">A line under it: the artists, or the playlist's description.</param>
    private sealed record Preview(string PageTitle, string Title, string? Description, Picture? Picture);

    /// <param name="Address">On the page's origin (a path), or the absolute address of a recording's cover.</param>
    /// <param name="Size">Its side, when known.</param>
    private sealed record Picture(string Address, int? Size);
}
