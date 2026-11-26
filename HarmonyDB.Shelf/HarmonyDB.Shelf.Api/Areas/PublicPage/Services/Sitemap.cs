using System.Text;
using System.Xml;
using HarmonyDB.Shelf.Api.Areas.App.Services;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.PublicPage.Services;

/// <summary>
/// The public page's sitemap: its main page, the showcase playlist, and the page of each of its songs, while the
/// playlist is open to guests. Nothing else of the page is for search engines.
/// </summary>
public sealed class Sitemap(ShelfDbContext db, PlaylistGate gate, IOptionsMonitor<PublicPageOptions> options, IOptionsMonitor<AppOptions> app)
{
    private const string Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    /// <summary>
    /// The sitemap's XML (UTF-8); null when there is no showcase.
    /// </summary>
    public async Task<byte[]?> GetAsync(CancellationToken cancellationToken)
    {
        var origin = options.CurrentValue.Origin!;
        if (app.CurrentValue.ShowcasePlaylist is not { } playlistId)
            return null;

        try
        {
            await gate.ReadAsync(null, playlistId, null, cancellationToken);
        }
        catch (ShelfException exception) when (exception.StatusCode == StatusCodes.Status404NotFound)
        {
            return null;
        }

        var songs = await db.PlaylistSongs.Where(x => x.PlaylistId == playlistId && x.DeletedAt == null)
            .OrderBy(x => x.Position).ThenBy(x => x.Id).Select(x => x.Id).ToListAsync(cancellationToken);

        using var stream = new MemoryStream();
        using (var xml = XmlWriter.Create(stream, new() { Encoding = new UTF8Encoding(false), Indent = true }))
        {
            xml.WriteStartDocument();
            xml.WriteStartElement("urlset", Namespace);
            foreach (var address in songs.Select(x => $"{origin}/songs/{x}").Prepend($"{origin}/"))
            {
                xml.WriteStartElement("url", Namespace);
                xml.WriteElementString("loc", Namespace, address);
                xml.WriteEndElement();
            }

            xml.WriteEndElement();
            xml.WriteEndDocument();
        }

        return stream.ToArray();
    }
}
