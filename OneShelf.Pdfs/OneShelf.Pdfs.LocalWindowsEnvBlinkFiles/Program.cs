using Syncfusion.HtmlConverter;
using Syncfusion.Pdf;

namespace OneShelf.Pdfs.LocalWindowsEnvBlinkFiles;

internal class Program
{
    static void Main()
    {
        //Initialize HTML to PDF converter with Blink rendering engine
        HtmlToPdfConverter htmlConverter = new HtmlToPdfConverter(HtmlRenderingEngine.Blink);

        //Convert URL to PDF
        PdfDocument document = htmlConverter.Convert("https://www.google.com");

        using var stream = new MemoryStream();

        //Save and close the PDF document 
        document.Save(stream);

        document.Close(true);

        Console.WriteLine(stream.Length);
    }
}