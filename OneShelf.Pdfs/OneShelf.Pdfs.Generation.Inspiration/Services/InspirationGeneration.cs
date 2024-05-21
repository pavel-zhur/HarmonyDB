using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Helpers;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.Database.Songs.Services;
using OneShelf.Pdfs.Generation.Inspiration.Helpers;
using OneShelf.Pdfs.Generation.Inspiration.Models;
using OneShelf.Pdfs.Generation.Resources;
using Document = iText.Layout.Document;

namespace OneShelf.Pdfs.Generation.Inspiration.Services;

public class InspirationGeneration
{
    private readonly ILogger<InspirationGeneration> _logger;
    private readonly SongsDatabase _songsDatabase;
    private readonly CategoriesCatalog _categoriesCatalog;

    private const int RegularSize = 10;
    private const int HeaderSize = 14;
    private const string AllCategoryTitle = "По порядку";

    private readonly PdfFont _light = CreateFont("OneShelf.Pdfs.Generation.Inspiration.Resources.Comfortaa-Light.ttf");
    private readonly PdfFont _semiBold = CreateFont("OneShelf.Pdfs.Generation.Inspiration.Resources.Comfortaa-SemiBold.ttf");
    private readonly PdfFont _bold = CreateFont("OneShelf.Pdfs.Generation.Inspiration.Resources.Comfortaa-Bold.ttf");
    private readonly SolidBorder _border = new(DeviceGray.MakeLighter(DeviceGray.GRAY), 1, .5f);

    private enum IndexReason
    {
        Main,
        Redirect,
        SameSongGroup,
        SameSongGroupRedirect,
    }

    private enum FragmentStyle
    {
        BlackBold,
        GraySemiBold,
        GrayLight,
    }

    private class ContentsTable
    {
        public required string Title { get; init; }

        public required IReadOnlyList<(IReadOnlyList<string> artists, string title, string? additionalKeywords, string chords, IReadOnlyList<(int index, IndexReason reason)> indices)> Songs { get; init; }
    }

    public InspirationGeneration(ILogger<InspirationGeneration> logger, SongsDatabase songsDatabase, CategoriesCatalog categoriesCatalog)
    {
        _logger = logger;
        _songsDatabase = songsDatabase;
        _categoriesCatalog = categoriesCatalog;
    }

    public async Task<byte[]> Inspiration(int tenantId, long userId)
    {
        var data = await GetData(tenantId, InspirationDataOrdering.ByArtist, userId);

        var pdfFile = await CreatePdf(data, InspirationDataOrdering.ByArtist, true, true);

        return pdfFile;
    }

    public async Task<byte[]> Inspiration(int tenantId, InspirationDataOrdering dataOrdering, bool withChords, bool compactArtists, bool onlyPublished, bool includeTitlePage = true, IReadOnlyList<int>? onlySongIndices = null, Dictionary<int, int>? indexesMap = null)
    {
        var data = await GetData(tenantId, dataOrdering, onlyPublished: onlyPublished, onlySongIndices: onlySongIndices, indexesMap: indexesMap);

        var pdfFile = await CreatePdf(data, dataOrdering, withChords, compactArtists, includeTitlePage);

        return pdfFile;
    }

    private static PdfFont CreateFont(string resourceName)
    {
        using var stream = typeof(InspirationGeneration).Assembly.GetManifestResourceStream(resourceName) ?? throw new($"The font resource {resourceName} is not found.");
        using var binaryReader = new BinaryReader(stream);
        return PdfFontFactory.CreateFont(binaryReader.ReadBytes((int)stream.Length), "Windows-1251");
    }
    
    private async Task<byte[]> CreatePdf(List<ContentsTable> data, InspirationDataOrdering dataOrdering, bool withChords, bool compactArtists, bool includeTitlePage = true)
    {
        using var pdfFile = new MemoryStream();
        await using var pdfWriter = new PdfWriter(pdfFile);
        using var pdfDoc = new PdfDocument(pdfWriter);
        using var doc = new Document(pdfDoc, PageSize.A4);

        if (includeTitlePage)
        {
            await AddFirstPage(doc, dataOrdering);
        }

        var shouldBreak = includeTitlePage;
        foreach (var contentsTable in data)
        {
            if (shouldBreak)
            {
                doc.Add(new(AreaBreakType.NEXT_PAGE));
            }

            shouldBreak = true;

            var widths = (withChords, compactArtists) switch
            {
                (_, true) => new[] { 4f, 8 },
                (true, _) => new[] { 4f, 5, 2, 1 },
                _ => new[] { 4f, 5, 2 }
            };

            var table = new Table(UnitValue.CreatePercentArray(widths));
            table.SetWidth(UnitValue.CreatePercentValue(100));

            AddHeader(table, contentsTable.Title, widths.Length);
            if (compactArtists)
            {
                foreach (var row in contentsTable.Songs.GroupBy(x => x.artists[0]))
                {
                    AddRow(table, row.Key, row, withChords);
                }
            }
            else
            {
                foreach (var (artists, title, additionalKeywords, chords, indices) in contentsTable.Songs)
                {
                    AddRow(table, string.Join(", ", artists), title, additionalKeywords, withChords ? chords : null, indices);
                }
            }

            doc.Add(table);
        }

        doc.Close();
        return pdfFile.ToArray();
    }

    private async Task AddFirstPage(Document doc, InspirationDataOrdering dataOrdering)
    {
        var div = new Div()
            .SetWidth(UnitValue.CreatePercentValue(100))
            .SetHeight(UnitValue.CreatePercentValue(70))
            .SetHorizontalAlignment(HorizontalAlignment.CENTER);

        div.Add(new Paragraph("Для вдохновения")
            .SetFont(_bold)
            .SetFontSize(30)
            .SetTextAlignment(TextAlignment.CENTER)
            .SetMarginTop(100));

        div.Add(new Paragraph($"Что сыграть? {dataOrdering.GetCaption()}")
            .SetFont(_bold)
            .SetFontSize(15)
            .SetTextAlignment(TextAlignment.CENTER));

        div.Add(new Paragraph(DateTime.Now.Year.ToString())
            .SetFont(_bold)
            .SetFontSize(15)
            .SetTextAlignment(TextAlignment.CENTER)
            .SetMarginTop(380));

        div.Add(new Image(ImageDataFactory.CreatePng(await PdfsGenerationResources.ReadQr()))
            .Scale(.2f, .2f)
            .SetHorizontalAlignment(HorizontalAlignment.CENTER));

        doc.Add(div);
    }

    private void AddRow(Table table, string artist,
        IEnumerable<(IReadOnlyList<string> artists, string title, string? additionalKeywords, string chords,
            IReadOnlyList<(int index, IndexReason reason)> indices)> songs, bool withChords)
    {
        AddCell(table, artist);

        var fragments = new List<(FragmentStyle style, string text)>();
        var isFirst = true;
        foreach (var (artists, title, additionalKeywords, chords, indices) in songs)
        {
            if (!isFirst)
            {
                fragments.Add((FragmentStyle.GrayLight, "; "));
            }

            isFirst = false;

            fragments.Add((FragmentStyle.BlackBold, title));

            if (indices.Any(x => x.reason == IndexReason.Main))
            {
                fragments.Add((FragmentStyle.GraySemiBold, $" {indices.Single(x => x.reason == IndexReason.Main).index}"));
            }

            if (GetAdditionalIndices(indices) is { } additionalIndices)
            {
                fragments.Add((FragmentStyle.GrayLight, additionalIndices));
            }

            if (withChords && !string.IsNullOrEmpty(chords))
            {
                fragments.Add((FragmentStyle.GrayLight, $" {chords}"));
            }

            if (artists.Count > 1 || additionalKeywords != null)
            {
                var additions = artists.Skip(1).Select(x => $"+{x}").ToList();
                if (additionalKeywords != null)
                    additions.Add(additionalKeywords);

                fragments.Add((FragmentStyle.GrayLight, $" ({string.Join(", ", additions)})"));
            }
        }

        AddCell(table, fragments);
    }

    private void AddCell(Table table, IEnumerable<(FragmentStyle style, string text)> fragments)
    {
        var paragraph = new Paragraph()
            .SetFontSize(RegularSize);

        foreach (var (style, text) in fragments)
        {
            paragraph.Add(new Text(text)
                .SetFont(style switch
                {
                    FragmentStyle.BlackBold => _bold,
                    FragmentStyle.GrayLight => _light,
                    FragmentStyle.GraySemiBold => _semiBold,
                    _ => throw new ArgumentOutOfRangeException(nameof(style))
                })
                .SetFontColor(style switch
                {
                    FragmentStyle.BlackBold => DeviceGray.BLACK,
                    FragmentStyle.GraySemiBold => DeviceGray.GRAY,
                    FragmentStyle.GrayLight => DeviceGray.GRAY,
                    _ => throw new ArgumentOutOfRangeException()
                }));
        }

        table.AddCell(
            new Cell()
                .Add(paragraph)
                .SetBorder(Border.NO_BORDER)
                .SetBorderTop(_border)
                .SetPaddingTop(0)
                .SetPaddingBottom(0)
                .SetTextAlignment(TextAlignment.LEFT)
                .SetKeepTogether(true));
    }

    private void AddRow(Table table, string author, string title, string? additionalKeywords, string? chords,
        IReadOnlyList<(int index, IndexReason reason)> indices)
    {
        AddCell(table, author);
        AddCell(table, additionalKeywords == null ? title : $"{title} ({additionalKeywords})");

        if (chords != null)
        {
            AddCell(table, null, andGrayValue: chords);
        }

        var additionalIndices = GetAdditionalIndices(indices);

        AddCell(table, indices.Where(x => x.reason == IndexReason.Main).Select(x => (int?)x.index).SingleOrDefault()?.ToString(), false, additionalIndices);
    }

    private static string? GetAdditionalIndices(IReadOnlyList<(int index, IndexReason reason)> indices)
    {
        var additionalIndices = string.Join(", ",
            indices.Where(x => x.reason != IndexReason.Main).Select(i => string.Format(i.reason switch
            {
                IndexReason.SameSongGroup => "{0}!",
                IndexReason.Redirect => "{0}",
                IndexReason.SameSongGroupRedirect => "{0}",
                IndexReason.Main => throw new ArgumentOutOfRangeException(),
                _ => throw new ArgumentOutOfRangeException()
            }, i.index)));
        additionalIndices = string.IsNullOrWhiteSpace(additionalIndices) ? null : $", {additionalIndices}";
        return additionalIndices;
    }

    private void AddCell(Table table, string? value, bool left = true, string? andGrayValue = null)
    {
        var paragraph = new Paragraph()
            .SetFont(_bold)
            .SetFontSize(RegularSize);

        if (value != null)
        {
            paragraph.Add(value);
        }

        if (andGrayValue != null)
        {
            paragraph.Add(
                new Text(andGrayValue)
                    .SetFontColor(DeviceGray.GRAY));
        }

        table.AddCell(
            new Cell()
                .Add(paragraph)
                .SetBorder(Border.NO_BORDER)
                .SetBorderTop(_border)
                .SetPaddingTop(0)
                .SetPaddingBottom(0)
                .SetTextAlignment(left ? TextAlignment.LEFT : TextAlignment.RIGHT)
                .SetKeepTogether(true));
    }

    private void AddHeader(Table table, string header, int colspan)
    {
        table.AddCell(
            new Cell(1, colspan)
                .Add(
                    new Paragraph(header)
                        .SetFont(_bold)
                        .SetFontSize(HeaderSize))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetBorder(Border.NO_BORDER));
    }

    private async Task<List<ContentsTable>> GetData(int tenantId, InspirationDataOrdering dataOrdering, long? onlyLikesOfUserId = null, bool onlyPublished = false, IReadOnlyList<int>? onlySongIndices = null, Dictionary<int, int>? indexesMap = null)
    {
        var songs = await _songsDatabase.Songs
            .Where(x => x.TenantId == tenantId)
            .SelectSingle(x => onlyPublished ? x.Where(x => x.Index < 1000) : x)
            .SelectSingle(x => onlySongIndices != null ? x.Where(x => onlySongIndices.Contains(x.Index)) : x)
            .SelectSingle(x => onlyLikesOfUserId.HasValue ? x.Where(x => x.Likes.Any(x => x.UserId == onlyLikesOfUserId)) : x)
            .Where(x => x.Status == SongStatus.Live)
            .Include(x => x.Artists)
            .ThenInclude(x => x.Synonyms)
            .Include(x => x.Versions)
            .Include(x => x.RedirectsFromSongs)
            .Include(x => x.SameSongGroup)
            .ThenInclude(x => x.Songs)
            .ToListAsync();

        IEnumerable<(Song s, string category, IReadOnlyList<string> artists)> rows;

        if (dataOrdering is InspirationDataOrdering.ByTitle or InspirationDataOrdering.ByIndexAsc or InspirationDataOrdering.ByIndexDesc)
        {
            rows = songs
                .SelectSingle(x => dataOrdering switch
                {
                    InspirationDataOrdering.ByIndexAsc => x.OrderBy(x => x.Index),
                    InspirationDataOrdering.ByIndexDesc => x.OrderByDescending(x => x.Index),
                    InspirationDataOrdering.ByTitle => x.OrderBy(x => x.Title).ThenBy(x => x.Index),
                    InspirationDataOrdering.ByArtistWithSynonyms
                        or InspirationDataOrdering.ByArtist
                        or _ => throw new ArgumentOutOfRangeException(nameof(dataOrdering), dataOrdering, null)
                })
                .Select(s =>
                {
                    string categoryTitle;
                    if (dataOrdering is InspirationDataOrdering.ByIndexAsc or InspirationDataOrdering.ByIndexDesc)
                    {
                        categoryTitle = AllCategoryTitle;
                    }
                    else
                    {
                        var category = s.CategoryOverride ?? s.Artists.Max(a => a.GetSongCategory());
                        if (category != SongCategory.Foreign) category = SongCategory.Domestic;
                        categoryTitle = _categoriesCatalog[category];
                    }

                    return (
                            s,
                            category: categoryTitle,
                            s.Artists.OrderBy(x => x.Name).Select(x => x.Name).ToList().AsIReadOnlyList());
                });
        }
        else
        {
            rows = songs
                .SelectMany(s => s.Artists.SelectMany(a =>
                {
                    var artistCategory = a.GetSongCategory();
                    return new[] { (a, artistTitle: a.Name, artistCategory) }
                        .SelectSingle(x => dataOrdering == InspirationDataOrdering.ByArtistWithSynonyms
                            ? x.Concat(a.Synonyms.Select(x => (a, artistTitle: x.Title, artistCategory)))
                            : x)
                        .Select(at => (
                            s,
                            category: _categoriesCatalog[s.CategoryOverride ?? at.artistCategory],
                            artists: at.artistTitle.Once().Concat(s.Artists.Where(a2 => a2 != a).Select(a2 => a2.Name).OrderBy(x => x)).ToList().AsIReadOnlyList()));
                }))
                .OrderBy(x => x.artists[0])
                .ThenBy(x => x.s.Title);
        }

        return rows
            .GroupBy(s => s.category)
            .Select(c => new ContentsTable
            {
                Title = c.Key,
                Songs = c
                    .Select(song =>
                    (
                        song.artists,
                        song.s.Title,
                        song.s.AdditionalKeywords,
                        string.Join(
                            ", ",
                            song.s.Versions
                                .Select(
                                    x => x.Uri.Host
                                        .SelectSingle(x => x.Substring(0, x.LastIndexOf('.')))
                                        .SelectSingle(x => x.Contains('.') ? x.Substring(x.LastIndexOf('.') + 1) : x))
                                .Distinct()
                                .OrderBy(x => x)
                                .Prepend(song.s.FileId != null ? "pdf" : null)
                                .Where(x => x != null)),
                        (indexesMap != null
                            ? (indexesMap[song.s.Index], IndexReason.Main).Once()
                            : (song.s.Index, IndexReason.Main)
                            .Once()
                            .Concat(song.s.RedirectsFromSongs.Select(
                                r => (r.Index, RedirectFromArchive: IndexReason.Redirect)))
                            .Concat(song.s.SameSongGroup?.Songs.Where(x => x != song.s).SelectMany(ssgs =>
                                        (ssgs.Index, IndexReason.SameSongGroup)
                                        .Once()
                                        .Concat(ssgs.RedirectsFromSongs.Select(r =>
                                            (r.Index, IndexReason.SameSongGroupRedirect))))
                                    ?? Enumerable.Empty<(int index, IndexReason reason)>()))
                        .ToList()
                        .AsIReadOnlyList()))
                    .ToList()
            })
            .OrderBy(x => x.Title)
            .ToList();
    }
}