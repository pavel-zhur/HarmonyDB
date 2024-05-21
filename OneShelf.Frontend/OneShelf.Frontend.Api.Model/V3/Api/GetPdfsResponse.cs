namespace OneShelf.Frontend.Api.Model.V3.Api;

public class GetPdfsResponse
{
    public Dictionary<string, Pdf> Pdfs { get; init; }

    public byte[]? Volume { get; init; }

    public class Pdf
    {
        public byte[]? PdfData { get; init; }

        public required int PageCount { get; init; }

        public string PreviewLink { get; set; }
    }
}