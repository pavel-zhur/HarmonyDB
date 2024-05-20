using OneShelf.Pdfs.Generation.Resources;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace OneShelf.Pdfs.Generation.Volumes.Services;

public class VolumesGeneration
{
    public async Task<(byte[] bytes, int total, int emptyAdded)> CreateDocument(IReadOnlyList<(byte[] doc, int? i)> volumeDocuments, string caption, string year)
    {
        GlobalFontSettings.FontResolver = MyFontResolver.Instance;

        using var result = new PdfDocument();

        var emptyRequired = false;

        var emptyAdded = 0;
        int? previousI = null;
        foreach (var (docBytes, i) in volumeDocuments)
        {
            using var docStream = new MemoryStream(docBytes);
            using var doc = PdfReader.Open(docStream, PdfDocumentOpenMode.Import);

            if (emptyRequired)
            {
                if (doc.Pages.Count > 1)
                {
                    var newPage = result.AddPage(new());
                    emptyAdded++;
                    if (previousI.HasValue)
                    {
                        AddAnnotation(newPage, previousI.Value);
                    }

                    emptyRequired = doc.Pages.Count % 2 == 1;
                }
                else
                {
                    emptyRequired = false;
                }
            }
            else
            {
                emptyRequired = doc.Pages.Count % 2 == 1;
            }

            var first = true;
            foreach (var page in doc.Pages)
            {
                if (first && doc.Pages.Count > 1 && result.Pages.Count % 2 != 0)
                {
                    throw new("could not have happened.");
                }

                var newPage = result.AddPage(page);
                if (i.HasValue)
                {
                    AddAnnotation(newPage, i.Value);
                }

                first = false;
            }

            previousI = i;
        }

        var total = result.PageCount;

        var title = result.InsertPage(0, new());

        await AddAnnotation(title, caption, year);

        using var stream = new MemoryStream();
        result.Save(stream);

        return (stream.ToArray(), total, emptyAdded);
    }

    private void AddAnnotation(PdfPage page, int i)
    {
        XFont font = new XFont(MyFontResolver.Consolas, 10);
        XBrush brush = XBrushes.Black;

        // Make a layout rectangle.
        XRect layoutRectangle = new XRect(255/*X*/, 30/*Y*/, page.Width/*Width*/, font.Height/*Height*/);

        using XGraphics gfx = XGraphics.FromPdfPage(page);
        gfx.DrawString(
            $"--{i:##000}--",
            font,
            brush,
            layoutRectangle,
            XStringFormats.Center);
    }

    private async Task AddAnnotation(PdfPage page, string caption, string year)
    {
        XFont font = new XFont(MyFontResolver.Consolas, 30);
        XFont font2 = new XFont(MyFontResolver.Consolas, 15);
        XBrush brush = XBrushes.Black;

        // Make a layout rectangle.
        XRect layoutRectangle = new XRect(0/*X*/, 100/*Y*/, page.Width/*Width*/, font.Height/*Height*/);
        var qrSize = 70;
        XRect imageRectangle = new XRect(page.Width / 2 - qrSize / 2, 690 - qrSize, qrSize, qrSize);
        XRect layoutRectangle2 = new XRect(0/*X*/, 700/*Y*/, page.Width/*Width*/, font.Height/*Height*/);

        using XGraphics gfx = XGraphics.FromPdfPage(page);
        gfx.DrawString(
            caption,
            font,
            brush,
            layoutRectangle,
            XStringFormats.Center);
        var bytes = await PdfsGenerationResources.ReadQr();
        using var stream = new MemoryStream(bytes, 0, bytes.Length, false, true);
        gfx.DrawImage(XImage.FromStream(stream), imageRectangle);
        gfx.DrawString(
            year,
            font2,
            brush,
            layoutRectangle2,
            XStringFormats.Center);
    }
}