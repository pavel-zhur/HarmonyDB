using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OneShelf.Common.Api;
using OneShelf.Pdfs.Generation.Volumes.Services;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using QRCoder;

namespace OneShelf.Pdfs.Api;

/// <summary>
/// A book of songs from their PDFs (each made by <see cref="Convert"/>), for internal clients with the scope
/// <see cref="PdfsModule.InternalScope"/>. With a caption, it is the book as the old shelf makes it: a title page with
/// the caption, the year and a QR code (to the address given), each song's number on its pages, and an empty page before a song of several pages where
/// one is needed for the song to open as a spread. Without one, the PDFs one after another.
/// The answer is the PDF, named "{page count}.pdf" as <see cref="Convert"/> names its. A request that cannot be read
/// is answered 400.
/// </summary>
public class Book(VolumesGeneration volumesGeneration)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Function(nameof(Book), AuthorizationLevel.Internal, "post", InternalScope = PdfsModule.InternalScope)]
    public async Task<IActionResult> Run(HttpRequest req, CancellationToken cancellationToken)
    {
        BookRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<BookRequest>(req.Body, Json, cancellationToken);
        }
        catch (JsonException)
        {
            return new BadRequestResult();
        }

        if (request?.Documents is not { Count: > 0 } documents || documents.Any(x => x?.Pdf is not { Length: > 0 }) || request.QrLink is { IsAbsoluteUri: false })
            return new BadRequestResult();

        byte[] bytes;
        if (!string.IsNullOrWhiteSpace(request.Caption))
        {
            bytes = (await volumesGeneration.CreateDocument(
                    documents.Select(x => (x.Pdf, x.Number)).ToList(),
                    request.Caption,
                    request.Year ?? string.Empty,
                    request.QrLink == null ? null : Qr(request.QrLink)))
                .bytes;
        }
        else
        {
            using var result = new PdfDocument();
            foreach (var document in documents)
            {
                using var stream = new MemoryStream(document.Pdf);
                using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
                foreach (var page in pdf.Pages)
                    result.AddPage(page);
            }

            using var output = new MemoryStream();
            result.Save(output);
            bytes = output.ToArray();
        }

        using var counted = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.InformationOnly);
        return new FileContentResult(bytes, "application/pdf")
        {
            FileDownloadName = $"{counted.PageCount}.pdf",
        };
    }

    /// <summary>
    /// The QR code of the address, as a BMP: the PDF library reads that one (not the PNGs the code library writes).
    /// </summary>
    private static byte[] Qr(Uri link)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(link.ToString(), QRCodeGenerator.ECCLevel.M);
        return new BitmapByteQRCode(data).GetGraphic(20);
    }

    public sealed record BookRequest
    {
        /// <summary>
        /// The title page's caption; none: no title page, no numbers, no empty pages.
        /// </summary>
        public string? Caption { get; init; }

        /// <summary>
        /// The line under the caption.
        /// </summary>
        public string? Year { get; init; }

        /// <summary>
        /// Where the QR code on the title page leads; none: the code this service always put there (the old shelf's).
        /// </summary>
        public Uri? QrLink { get; init; }

        public required List<BookDocument> Documents { get; init; }
    }

    /// <param name="Pdf">The song's PDF.</param>
    /// <param name="Number">The song's number, put on its pages in a book with a caption; none for a page of contents.</param>
    public sealed record BookDocument(byte[] Pdf, int? Number);
}
